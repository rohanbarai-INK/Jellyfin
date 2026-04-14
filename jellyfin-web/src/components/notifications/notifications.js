import serverNotifications from '../../scripts/serverNotifications';
import { playbackManager } from '../playback/playbackmanager';
import Events from '../../utils/events.ts';
import globalize from '../../lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { getItems } from '../../utils/jellyfin-apiclient/getItems.ts';
import {
    getContentRequestWebPushPublicKey,
    subscribeContentRequestWebPush
} from '../../utils/contentRequestsApi';

import NotificationIcon from './notificationicon.png';

function onOneDocumentClick() {
    document.removeEventListener('click', onOneDocumentClick);
    document.removeEventListener('keydown', onOneDocumentClick);

    // don't request notification permissions if they're already granted or denied
    if (window.Notification && window.Notification.permission === 'default') {
        const permissionResult = Notification.requestPermission();
        if (permissionResult && typeof permissionResult.then === 'function') {
            permissionResult
                .then(permission => {
                    if (permission === 'granted') {
                        void ensureRequestWebPushSubscription();
                    }
                })
                .catch(() => {
                    // Ignore permission request failures.
                });
        }
    } else if (window.Notification && window.Notification.permission === 'granted') {
        void ensureRequestWebPushSubscription();
    }
}

function registerOneDocumentClickHandler() {
    Events.off(ServerConnections, 'localusersignedin', registerOneDocumentClickHandler);

    document.addEventListener('click', onOneDocumentClick);
    document.addEventListener('keydown', onOneDocumentClick);
}

function initPermissionRequest() {
    const apiClient = ServerConnections.currentApiClient();
    if (apiClient) {
        apiClient.getCurrentUser()
            .then(() => registerOneDocumentClickHandler())
            .catch(() => {
                Events.on(ServerConnections, 'localusersignedin', registerOneDocumentClickHandler);
            });
    } else {
        registerOneDocumentClickHandler();
    }
}

initPermissionRequest();

let serviceWorkerRegistration;
let requestWebPushSyncInFlight = false;
let requestWebPushSubscriptionFingerprint = '';

function closeAfter(notification, timeoutMs) {
    setTimeout(function () {
        if (notification.close) {
            notification.close();
        } else if (notification.cancel) {
            notification.cancel();
        }
    }, timeoutMs);
}

function resetRegistration() {
    /* eslint-disable-next-line compat/compat */
    const serviceWorker = navigator.serviceWorker;
    if (serviceWorker) {
        serviceWorker.ready.then(function (registration) {
            serviceWorkerRegistration = registration;
        });
    }
}

resetRegistration();

function showPersistentNotification(title, options) {
    serviceWorkerRegistration.showNotification(title, options);
}

function showNonPersistentNotification(title, options, timeoutMs) {
    try {
        const notif = new Notification(title, options); /* eslint-disable-line compat/compat */

        if (notif.show) {
            notif.show();
        }

        if (timeoutMs) {
            closeAfter(notif, timeoutMs);
        }
    } catch (err) {
        if (options.actions) {
            options.actions = [];
            showNonPersistentNotification(title, options, timeoutMs);
        } else {
            throw err;
        }
    }
}

function showNotification(options, timeoutMs, apiClient) {
    const title = options.title;

    options.data = options.data || {};
    options.data.serverId = apiClient.serverInfo().Id;
    options.icon = options.icon || NotificationIcon;
    options.badge = options.badge || NotificationIcon;

    resetRegistration();

    if (serviceWorkerRegistration) {
        showPersistentNotification(title, options);
        return;
    }

    showNonPersistentNotification(title, options, timeoutMs);
}

function isRequestWebPushSupported() {
    return Boolean(window.Notification && window.PushManager && navigator.serviceWorker);
}

function urlBase64ToUint8Array(base64String) {
    const paddedBase64 = base64String + '='.repeat((4 - (base64String.length % 4)) % 4);
    const normalizedBase64 = paddedBase64.replace(/-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(normalizedBase64);
    const outputArray = new Uint8Array(rawData.length);

    for (let index = 0; index < rawData.length; index++) {
        outputArray[index] = rawData.charCodeAt(index);
    }

    return outputArray;
}

function getPushSubscriptionKeys(subscription) {
    if (!subscription || typeof subscription.toJSON !== 'function') {
        return null;
    }

    const json = subscription.toJSON();
    const p256dh = json?.keys?.p256dh;
    const auth = json?.keys?.auth;
    if (!p256dh || !auth) {
        return null;
    }

    return { p256dh, auth };
}

async function ensureRequestWebPushSubscription(apiClientOverride) {
    const apiClient = apiClientOverride || ServerConnections.currentApiClient();
    if (!apiClient || !apiClient.getCurrentUserId() || requestWebPushSyncInFlight) {
        return;
    }

    if (!isRequestWebPushSupported() || window.Notification.permission !== 'granted') {
        return;
    }

    requestWebPushSyncInFlight = true;

    try {
        const registration = serviceWorkerRegistration || await navigator.serviceWorker.ready;
        if (!registration || !registration.pushManager) {
            return;
        }

        serviceWorkerRegistration = registration;

        const publicKey = await getContentRequestWebPushPublicKey(apiClient);
        if (!publicKey) {
            return;
        }

        let subscription = await registration.pushManager.getSubscription();
        if (!subscription) {
            subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(publicKey)
            });
        }

        const endpoint = subscription?.endpoint;
        const keys = getPushSubscriptionKeys(subscription);
        if (!endpoint || !keys) {
            return;
        }

        const subscriptionFingerprint = `${apiClient.serverId()}:${apiClient.getCurrentUserId()}:${endpoint}`;
        if (requestWebPushSubscriptionFingerprint === subscriptionFingerprint) {
            return;
        }

        await subscribeContentRequestWebPush({
            endpoint,
            p256dh: keys.p256dh,
            auth: keys.auth
        }, apiClient);

        requestWebPushSubscriptionFingerprint = subscriptionFingerprint;
    } catch (error) {
        console.error('[notifications] failed to register request web push subscription', error);
    } finally {
        requestWebPushSyncInFlight = false;
    }
}

function showNewItemNotification(item, apiClient) {
    if (playbackManager.isPlayingLocally(['Video'])) {
        return;
    }

    let body = item.Name;

    if (item.SeriesName) {
        body = item.SeriesName + ' - ' + body;
    }

    const notification = {
        title: 'New ' + item.Type,
        body: body,
        vibrate: true,
        tag: 'newItem' + item.Id,
        data: {}
    };

    const imageTags = item.ImageTags || {};

    if (imageTags.Primary) {
        notification.icon = apiClient.getScaledImageUrl(item.Id, {
            width: 80,
            tag: imageTags.Primary,
            type: 'Primary'
        });
    }

    showNotification(notification, 15000, apiClient);
}

function onLibraryChanged(data, apiClient) {
    const newItems = data.ItemsAdded;

    if (!newItems.length) {
        return;
    }

    // Don't put a massive number of Id's onto the query string
    if (newItems.length > 12) {
        newItems.length = 12;
    }

    getItems(apiClient, apiClient.getCurrentUserId(), {

        Recursive: true,
        Limit: 3,
        Filters: 'IsNotFolder',
        SortBy: 'DateCreated',
        SortOrder: 'Descending',
        Ids: newItems.join(','),
        MediaTypes: 'Audio,Video',
        EnableTotalRecordCount: false

    }).then(function (result) {
        const items = result.Items;

        for (const item of items) {
            showNewItemNotification(item, apiClient);
        }
    });
}

function showPackageInstallNotification(apiClient, installation, status) {
    apiClient.getCurrentUser().then(function (user) {
        if (!user.Policy.IsAdministrator) {
            return;
        }

        const notification = {
            tag: 'install' + installation.Id,
            data: {}
        };

        if (status === 'completed') {
            notification.title = globalize.translate('PackageInstallCompleted', installation.Name, installation.Version);
            notification.vibrate = true;
        } else if (status === 'cancelled') {
            notification.title = globalize.translate('PackageInstallCancelled', installation.Name, installation.Version);
        } else if (status === 'failed') {
            notification.title = globalize.translate('PackageInstallFailed', installation.Name, installation.Version);
            notification.vibrate = true;
        } else if (status === 'progress') {
            notification.title = globalize.translate('InstallingPackage', installation.Name, installation.Version);

            notification.actions =
                [
                    {
                        action: 'cancel-install',
                        title: globalize.translate('ButtonCancel'),
                        icon: NotificationIcon
                    }
                ];

            notification.data.id = installation.id;
        }

        if (status === 'progress') {
            const percentComplete = Math.round(installation.PercentComplete || 0);

            notification.body = percentComplete + '% complete.';
        }

        const timeout = status === 'cancelled' ? 5000 : 0;

        showNotification(notification, timeout, apiClient);
    });
}

Events.on(ServerConnections, 'localusersignedin', function (_e, user) {
    const apiClient = user?.ServerId
        ? ServerConnections.getApiClient(user.ServerId)
        : ServerConnections.currentApiClient();

    void ensureRequestWebPushSubscription(apiClient);
});

Events.on(ServerConnections, 'localusersignedout', function () {
    requestWebPushSubscriptionFingerprint = '';
});

(function initRequestWebPush() {
    const apiClient = ServerConnections.currentApiClient();
    if (!apiClient || !apiClient.getCurrentUserId()) {
        return;
    }

    void ensureRequestWebPushSubscription(apiClient);
})();

Events.on(serverNotifications, 'LibraryChanged', function (e, apiClient, data) {
    onLibraryChanged(data, apiClient);
});

Events.on(serverNotifications, 'PackageInstallationCompleted', function (e, apiClient, data) {
    showPackageInstallNotification(apiClient, data, 'completed');
});

Events.on(serverNotifications, 'PackageInstallationFailed', function (e, apiClient, data) {
    showPackageInstallNotification(apiClient, data, 'failed');
});

Events.on(serverNotifications, 'PackageInstallationCancelled', function (e, apiClient, data) {
    showPackageInstallNotification(apiClient, data, 'cancelled');
});

Events.on(serverNotifications, 'PackageInstalling', function (e, apiClient, data) {
    showPackageInstallNotification(apiClient, data, 'progress');
});

Events.on(serverNotifications, 'ServerShuttingDown', function (e, apiClient) {
    const serverId = apiClient.serverInfo().Id;
    const notification = {
        tag: 'restart' + serverId,
        title: globalize.translate('ServerNameIsShuttingDown', apiClient.serverInfo().Name)
    };
    showNotification(notification, 0, apiClient);
});

Events.on(serverNotifications, 'ServerRestarting', function (e, apiClient) {
    const serverId = apiClient.serverInfo().Id;
    const notification = {
        tag: 'restart' + serverId,
        title: globalize.translate('ServerNameIsRestarting', apiClient.serverInfo().Name)
    };
    showNotification(notification, 0, apiClient);
});

Events.on(serverNotifications, 'RestartRequired', function (e, apiClient) {
    const serverId = apiClient.serverInfo().Id;
    const notification = {
        tag: 'restart' + serverId,
        title: globalize.translate('PleaseRestartServerName', apiClient.serverInfo().Name)
    };

    notification.actions =
        [
            {
                action: 'restart',
                title: globalize.translate('Restart'),
                icon: NotificationIcon
            }
        ];

    showNotification(notification, 0, apiClient);
});

