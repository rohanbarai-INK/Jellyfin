/* eslint-env serviceworker */

function getApiClient(serverId) {
    if (typeof window === 'undefined' || !window.connectionManager) {
        return Promise.reject(new Error('Connection manager is not available in service worker context.'));
    }

    return Promise.resolve(window.connectionManager.getApiClient(serverId));
}

function getScopeUrl() {
    if (self.registration && self.registration.scope) {
        return self.registration.scope;
    }

    return self.location.origin + '/';
}

function getNotificationTargetUrl(data) {
    if (data && typeof data.requestTargetUrl === 'string' && data.requestTargetUrl.length) {
        return data.requestTargetUrl;
    }

    if (data && typeof data.requestTargetPath === 'string' && data.requestTargetPath.length) {
        const scopeUrl = new URL(getScopeUrl());
        if (data.requestTargetPath.startsWith('#')) {
            return `${scopeUrl.origin}${scopeUrl.pathname}${data.requestTargetPath}`;
        }

        if (data.requestTargetPath.startsWith('/')) {
            return `${scopeUrl.origin}${data.requestTargetPath}`;
        }

        return `${scopeUrl.origin}${scopeUrl.pathname}${data.requestTargetPath}`;
    }

    return getScopeUrl();
}

function openNotificationWindow(data) {
    return clients.openWindow(getNotificationTargetUrl(data));
}

function readPushPayload(event) {
    if (!event.data) {
        return null;
    }

    try {
        return event.data.json();
    } catch (error) {
        return null;
    }
}

/* eslint-disable-next-line no-restricted-globals -- self is valid in a serviceworker environment */
self.addEventListener('push', function (event) {
    const payload = readPushPayload(event) || {};
    const title = typeof payload.title === 'string' && payload.title.length
        ? payload.title
        : 'Request Ready';

    const options = {
        body: typeof payload.body === 'string' ? payload.body : '',
        tag: typeof payload.tag === 'string' ? payload.tag : undefined,
        requireInteraction: true,
        data: payload.data && typeof payload.data === 'object' ? payload.data : {}
    };

    if (typeof payload.icon === 'string' && payload.icon.length) {
        options.icon = payload.icon;
    }

    if (typeof payload.badge === 'string' && payload.badge.length) {
        options.badge = payload.badge;
    }

    event.waitUntil(self.registration.showNotification(title, options));
});

function executeAction(action, data, serverId) {
    return getApiClient(serverId).then(function (apiClient) {
        switch (action) {
            case 'cancel-install':
                return apiClient.cancelPackageInstallation(data.id);
            case 'restart':
                return apiClient.restartServer();
            case 'open-request-item':
                return openNotificationWindow(data);
            default:
                return openNotificationWindow(data);
        }
    }).catch(function () {
        return openNotificationWindow(data);
    });
}

/* eslint-disable-next-line no-restricted-globals -- self is valid in a serviceworker environment */
self.addEventListener('notificationclick', function (event) {
    const notification = event.notification;
    notification.close();

    const data = notification.data || {};
    const serverId = data.serverId;
    const action = event.action;

    if (!action) {
        event.waitUntil(openNotificationWindow(data));
        return;
    }

    event.waitUntil(executeAction(action, data, serverId));
}, false);

/* eslint-disable-next-line no-restricted-globals -- self is valid in a serviceworker environment */
self.addEventListener('activate', () => self.clients.claim());
