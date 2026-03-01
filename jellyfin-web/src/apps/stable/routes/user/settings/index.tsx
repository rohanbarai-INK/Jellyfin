import type { UserDto } from '@jellyfin/sdk/lib/generated-client/models/user-dto';
import React, { useCallback, useEffect, useMemo, useState, type FC } from 'react';
import { useSearchParams } from 'react-router-dom';

import { appHost } from 'components/apphost';
import layoutManager from 'components/layoutManager';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import { AppFeature } from 'constants/appFeature';
import LinkButton from 'elements/emby-button/LinkButton';
import Button from 'elements/emby-button/Button';
import Input from 'elements/emby-input/Input';
import { useApi } from 'hooks/useApi';
import { useQuickConnectEnabled } from 'hooks/useQuickConnect';
import { useUsers } from 'hooks/useUsers';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import globalize from 'lib/globalize';
import browser from 'scripts/browser';
import Dashboard from 'utils/dashboard';
import shell from 'scripts/shell';
import keyboardNavigation from 'scripts/keyboardNavigation';

type UserDtoWithSubscription = UserDto & {
    ExpiryDate?: string | null;
    Status?: string | null;
};

const getServerErrorMessage = async (err: unknown): Promise<string | undefined> => {
    if (err instanceof Response) {
        if (err.status === 401) {
            return 'Unauthorized request. Sign out and sign in again, then retry.';
        }

        if (err.status === 404) {
            return 'Redeem endpoint not found. Make sure the backend is running the new access key code.';
        }

        const responseText = await err.text();
        if (responseText.trim()) {
            return responseText;
        }

        return undefined;
    }

    const error = err as {
        status?: number;
        text?: () => Promise<string>;
        response?: { status?: number; data?: unknown };
    };
    const statusCode = error.response?.status ?? error.status;
    const responseData = error.response?.data;

    if (statusCode === 401) {
        return 'Unauthorized request. Sign out and sign in again, then retry.';
    }

    if (statusCode === 404) {
        return 'Redeem endpoint not found. Make sure the backend is running the new access key code.';
    }

    if (typeof error.text === 'function') {
        const responseText = await error.text();
        if (responseText.trim()) {
            return responseText;
        }
    }

    if (typeof responseData === 'string' && responseData.trim()) {
        return responseData;
    }

    if (responseData && typeof responseData === 'object') {
        const payload = responseData as Record<string, unknown>;
        const message = payload.message ?? payload.Message ?? payload.error ?? payload.Error;
        if (typeof message === 'string' && message.trim()) {
            return message;
        }
    }

    return undefined;
};

const formatExpiryDate = (expiryDate?: string | null) => {
    if (!expiryDate) {
        return 'Not set';
    }

    const parsedDate = new Date(expiryDate);
    if (Number.isNaN(parsedDate.getTime())) {
        return expiryDate;
    }

    return parsedDate.toLocaleString();
};

const UserSettingsPage: FC = () => {
    const { user: currentUser } = useApi();
    const [ searchParams ] = useSearchParams();
    const {
        data: isQuickConnectEnabled,
        isPending: isQuickConnectEnabledPending
    } = useQuickConnectEnabled();
    const { data: users } = useUsers();
    const [ user, setUser ] = useState<UserDto>();
    const [ accessKey, setAccessKey ] = useState('');
    const [ isRedeemingKey, setIsRedeemingKey ] = useState(false);
    const [ redeemErrorMessage, setRedeemErrorMessage ] = useState('');
    const [ redeemSuccessMessage, setRedeemSuccessMessage ] = useState('');

    const userId = useMemo(() => (
        searchParams.get('userId') || currentUser?.Id
    ), [ currentUser, searchParams ]);
    const isLoggedInUser = useMemo(() => (
        userId && userId === currentUser?.Id
    ), [ currentUser, userId ]);

    useEffect(() => {
        if (userId) {
            if (userId === currentUser?.Id) setUser(currentUser);
            else setUser(users?.find(({ Id }) => userId === Id));
        }
    }, [ currentUser, userId, users ]);

    const onRedeemAccessKey = useCallback(async () => {
        if (!isLoggedInUser || !accessKey.trim()) {
            return;
        }

        const apiClient = ServerConnections.currentApiClient();
        if (!apiClient) {
            setRedeemErrorMessage('Unable to reach server API.');
            return;
        }

        setRedeemErrorMessage('');
        setRedeemSuccessMessage('');
        setIsRedeemingKey(true);
        try {
            const response = await apiClient.ajax({
                type: 'POST',
                url: apiClient.getUrl('Keys/Redeem'),
                data: JSON.stringify({
                    Key: accessKey.trim()
                }),
                dataType: 'json',
                contentType: 'application/json'
            });

            const responseData = response as { ExpiryDate?: string | null; expiryDate?: string | null };
            const redeemedExpiryDate = responseData.ExpiryDate ?? responseData.expiryDate;
            setUser(prevUser => {
                if (!prevUser) {
                    return prevUser;
                }

                return {
                    ...(prevUser as UserDtoWithSubscription),
                    ExpiryDate: redeemedExpiryDate,
                    Status: 'Active'
                } as UserDto;
            });

            setAccessKey('');
            setRedeemSuccessMessage('Access key redeemed successfully.');
        } catch (err) {
            console.error('[usersettings] failed to redeem access key', err);
            setRedeemErrorMessage((await getServerErrorMessage(err)) || 'Failed to redeem access key. Check the key and try again.');
        } finally {
            setIsRedeemingKey(false);
        }
    }, [ isLoggedInUser, accessKey ]);

    const onAccessKeyChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setAccessKey(event.target.value);
    }, []);

    if (!userId || !user || isQuickConnectEnabledPending) {
        return (
            <Loading />
        );
    }

    const subscriptionUser = user as UserDtoWithSubscription;

    // gamepad toggle unavailable on EdgeUWP, and smoothscroll unavailable on non-TV layout
    const isControlsPageEmpty = !keyboardNavigation.canEnableGamepad() && !layoutManager.tv;

    return (
        <Page
            id='myPreferencesMenuPage'
            className='libraryPage userPreferencesPage noSecondaryNavPage mainAnimatedPage'
            title={globalize.translate('Settings')}
            shouldAutoFocus
        >
            <div className='padded-left padded-right padded-bottom-page padded-top'>
                <div
                    className='readOnlyContent'
                    style={{
                        margin: '0 auto'
                    }}
                >
                    <div className='verticalSection verticalSection-extrabottompadding'>
                        <h2
                            className='sectionTitle headerUsername'
                            style={{
                                paddingLeft: '0.25em'
                            }}
                        >
                            {user.Name}
                        </h2>

                        <LinkButton
                            href={`#/userprofile?userId=${userId}`}
                            className='lnkUserProfile listItem-border'
                            style={{
                                display: 'block',
                                margin: 0,
                                padding: 0
                            }}
                        >
                            <div className='listItem'>
                                <span className='material-icons listItemIcon listItemIcon-transparent person' aria-hidden='true' />
                                <div className='listItemBody'>
                                    <div className='listItemBodyText'>
                                        {globalize.translate('Profile')}
                                    </div>
                                </div>
                            </div>
                        </LinkButton>

                        {isLoggedInUser && (
                            <LinkButton
                                href={`#/personalinsights?period=month&userId=${userId}`}
                                className='lnkPersonalInsights listItem-border'
                                style={{
                                    display: 'block',
                                    margin: 0,
                                    padding: 0
                                }}
                            >
                                <div className='listItem'>
                                    <span className='material-icons listItemIcon listItemIcon-transparent insights' aria-hidden='true' />
                                    <div className='listItemBody'>
                                        <div className='listItemBodyText'>
                                            Personal Insights
                                        </div>
                                    </div>
                                </div>
                            </LinkButton>
                        )}

                        {isQuickConnectEnabled && (
                            <LinkButton
                                href={`#/quickconnect?userId=${userId}`}
                                className='lnkQuickConnectPreferences listItem-border'
                                style={{
                                    display: 'block',
                                    margin: 0,
                                    padding: 0
                                }}
                            >
                                <div className='listItem'>
                                    <span className='material-icons listItemIcon listItemIcon-transparent phonelink_lock' aria-hidden='true' />
                                    <div className='listItemBody'>
                                        <div className='listItemBodyText'>
                                            {globalize.translate('QuickConnect')}
                                        </div>
                                    </div>
                                </div>
                            </LinkButton>
                        )}

                        <LinkButton
                            href={`#/mypreferencesdisplay?userId=${userId}`}
                            className='lnkDisplayPreferences listItem-border'
                            style={{
                                display: 'block',
                                margin: 0,
                                padding: 0
                            }}
                        >
                            <div className='listItem'>
                                <span className='material-icons listItemIcon listItemIcon-transparent tv' aria-hidden='true' />
                                <div className='listItemBody'>
                                    <div className='listItemBodyText'>
                                        {globalize.translate('Display')}
                                    </div>
                                </div>
                            </div>
                        </LinkButton>

                        <LinkButton
                            href={`#/mypreferenceshome?userId=${userId}`}
                            className='lnkHomePreferences listItem-border'
                            style={{
                                display: 'block',
                                margin: 0,
                                padding: 0
                            }}
                        >
                            <div className='listItem'>
                                <span className='material-icons listItemIcon listItemIcon-transparent home' aria-hidden='true' />
                                <div className='listItemBody'>
                                    <div className='listItemBodyText'>
                                        {globalize.translate('Home')}
                                    </div>
                                </div>
                            </div>
                        </LinkButton>

                        <LinkButton
                            href={`#/mypreferencesplayback?userId=${userId}`}
                            className='lnkPlaybackPreferences listItem-border'
                            style={{
                                display: 'block',
                                margin: 0,
                                padding: 0
                            }}
                        >
                            <div className='listItem'>
                                <span className='material-icons listItemIcon listItemIcon-transparent play_circle_filled' aria-hidden='true' />
                                <div className='listItemBody'>
                                    <div className='listItemBodyText'>
                                        {globalize.translate('TitlePlayback')}
                                    </div>
                                </div>
                            </div>
                        </LinkButton>

                        <LinkButton
                            href={`#/mypreferencessubtitles?userId=${userId}`}
                            className='lnkSubtitlePreferences listItem-border'
                            style={{
                                display: 'block',
                                margin: 0,
                                padding: 0
                            }}
                        >
                            <div className='listItem'>
                                <span className='material-icons listItemIcon listItemIcon-transparent closed_caption' aria-hidden='true' />
                                <div className='listItemBody'>
                                    <div className='listItemBodyText'>
                                        {globalize.translate('Subtitles')}
                                    </div>
                                </div>
                            </div>
                        </LinkButton>

                        <LinkButton
                            href='#/request'
                            className='lnkContentRequests listItem-border'
                            style={{
                                display: 'block',
                                margin: 0,
                                padding: 0
                            }}
                        >
                            <div className='listItem'>
                                <span className='material-icons listItemIcon listItemIcon-transparent live_help' aria-hidden='true' />
                                <div className='listItemBody'>
                                    <div className='listItemBodyText'>
                                        {globalize.translate('RequestMenuLabel')}
                                    </div>
                                </div>
                            </div>
                        </LinkButton>

                        {appHost.supports(AppFeature.DownloadManagement) && (
                            <LinkButton
                                onClick={shell.openDownloadManager}
                                className='downloadManager listItem-border'
                                style={{
                                    display: 'block',
                                    margin: 0,
                                    padding: 0
                                }}
                            >
                                <div className='listItem'>
                                    <span className='material-icons listItemIcon listItemIcon-transparent download' aria-hidden='true' />
                                    <div className='listItemBody'>
                                        <div className='listItemBodyText'>
                                            {globalize.translate('DownloadManager')}
                                        </div>
                                    </div>
                                </div>
                            </LinkButton>
                        )}

                        {appHost.supports(AppFeature.ClientSettings) && (
                            <LinkButton
                                onClick={shell.openClientSettings}
                                className='clientSettings listItem-border'
                                style={{
                                    display: 'block',
                                    margin: 0,
                                    padding: 0
                                }}
                            >
                                <div className='listItem'>
                                    <span className='material-icons listItemIcon listItemIcon-transparent devices_other' aria-hidden='true' />
                                    <div className='listItemBody'>
                                        <div className='listItemBodyText'>
                                            {globalize.translate('ClientSettings')}
                                        </div>
                                    </div>
                                </div>
                            </LinkButton>
                        )}

                        {isLoggedInUser && !browser.mobile && !isControlsPageEmpty && (
                            <LinkButton
                                href={`#/mypreferencescontrols?userId=${userId}`}
                                className='lnkControlsPreferences listItem-border'
                                style={{
                                    display: 'block',
                                    margin: 0,
                                    padding: 0
                                }}
                            >
                                <div className='listItem'>
                                    <span className='material-icons listItemIcon listItemIcon-transparent keyboard' aria-hidden='true' />
                                    <div className='listItemBody'>
                                        <div className='listItemBodyText'>
                                            {globalize.translate('Controls')}
                                        </div>
                                    </div>
                                </div>
                            </LinkButton>
                        )}
                    </div>

                    <div className='verticalSection verticalSection-extrabottompadding'>
                        <h2
                            className='sectionTitle headerUsername'
                            style={{
                                paddingLeft: '0.25em'
                            }}
                        >
                            Subscription Status
                        </h2>

                        <div className='listItem'>
                            <span className='material-icons listItemIcon listItemIcon-transparent event' aria-hidden='true' />
                            <div className='listItemBody'>
                                <div className='listItemBodyText'>
                                    Expiry Date: {formatExpiryDate(subscriptionUser.ExpiryDate)}
                                </div>
                                <div className='listItemBodyText secondary'>
                                    Status: {subscriptionUser.Status || 'Active'}
                                </div>
                            </div>
                        </div>

                        {isLoggedInUser && (
                            <div
                                style={{
                                    padding: '0.5em 0.75em'
                                }}
                            >
                                <div className='inputContainer'>
                                    <Input
                                        id='txtRedeemAccessKey'
                                        type='text'
                                        value={accessKey}
                                        label='Redeem Access Key'
                                        placeholder='JF-XXXX-XXXX'
                                        onChange={onAccessKeyChange}
                                    />
                                </div>
                                <Button
                                    type='button'
                                    className='raised button-submit'
                                    title={isRedeemingKey ? 'Redeeming...' : 'Redeem Key'}
                                    onClick={onRedeemAccessKey}
                                    disabled={isRedeemingKey || !accessKey.trim()}
                                />
                                {!!redeemSuccessMessage && (
                                    <div style={{ marginTop: '0.5em' }}>
                                        {redeemSuccessMessage}
                                    </div>
                                )}
                                {!!redeemErrorMessage && (
                                    <div style={{ marginTop: '0.5em', color: 'var(--error)' }}>
                                        {redeemErrorMessage}
                                    </div>
                                )}
                            </div>
                        )}
                    </div>

                    {isLoggedInUser && user.Policy?.IsAdministrator && !layoutManager.tv && (
                        <div className='adminSection verticalSection verticalSection-extrabottompadding'>
                            <h2
                                className='sectionTitle headerUsername'
                                style={{
                                    paddingLeft: '0.25em'
                                }}
                            >
                                {globalize.translate('HeaderAdmin')}
                            </h2>

                            <LinkButton
                                href='#/dashboard'
                                className='listItem-border'
                                style={{
                                    display: 'block',
                                    margin: 0,
                                    padding: 0
                                }}
                            >
                                <div className='listItem'>
                                    <span className='material-icons listItemIcon listItemIcon-transparent dashboard' aria-hidden='true' />
                                    <div className='listItemBody'>
                                        <div className='listItemBodyText'>
                                            {globalize.translate('TabDashboard')}
                                        </div>
                                    </div>
                                </div>
                            </LinkButton>

                            <LinkButton
                                href='#/metadata'
                                className='listItem-border'
                                style={{
                                    display: 'block',
                                    margin: 0,
                                    padding: 0
                                }}
                            >
                                <div className='listItem'>
                                    <span className='material-icons listItemIcon listItemIcon-transparent mode_edit' aria-hidden='true' />
                                    <div className='listItemBody'>
                                        <div className='listItemBodyText'>
                                            {globalize.translate('MetadataManager')}
                                        </div>
                                    </div>
                                </div>
                            </LinkButton>
                        </div>
                    )}

                    {isLoggedInUser && (
                        <div className='userSection verticalSection verticalSection-extrabottompadding'>
                            <h2
                                className='sectionTitle headerUsername'
                                style={{
                                    paddingLeft: '0.25em'
                                }}
                            >
                                {globalize.translate('HeaderUser')}
                            </h2>

                            {appHost.supports(AppFeature.MultiServer) && (
                                <LinkButton
                                    onClick={Dashboard.selectServer}
                                    className='selectServer listItem-border'
                                    style={{
                                        display: 'block',
                                        margin: 0,
                                        padding: 0
                                    }}
                                >
                                    <div className='listItem'>
                                        <span className='material-icons listItemIcon listItemIcon-transparent storage' aria-hidden='true' />
                                        <div className='listItemBody'>
                                            <div className='listItemBodyText'>
                                                {globalize.translate('SelectServer')}
                                            </div>
                                        </div>
                                    </div>
                                </LinkButton>
                            )}

                            <LinkButton
                                onClick={Dashboard.logout}
                                className='btnLogout listItem-border'
                                style={{
                                    display: 'block',
                                    margin: 0,
                                    padding: 0
                                }}
                            >
                                <div className='listItem'>
                                    <span className='material-icons listItemIcon listItemIcon-transparent exit_to_app' aria-hidden='true' />
                                    <div className='listItemBody'>
                                        <div className='listItemBodyText'>
                                            {globalize.translate('ButtonSignOut')}
                                        </div>
                                    </div>
                                </div>
                            </LinkButton>

                            {appHost.supports(AppFeature.ExitMenu) && (
                                <LinkButton
                                    onClick={appHost.exit}
                                    className='exitApp listItem-border'
                                    style={{
                                        display: 'block',
                                        margin: 0,
                                        padding: 0
                                    }}
                                >
                                    <div className='listItem'>
                                        <span className='material-icons listItemIcon listItemIcon-transparent close' aria-hidden='true' />
                                        <div className='listItemBody'>
                                            <div className='listItemBodyText'>
                                                {globalize.translate('ButtonExitApp')}
                                            </div>
                                        </div>
                                    </div>
                                </LinkButton>
                            )}
                        </div>
                    )}
                </div>
            </div>
        </Page>
    );
};

export default UserSettingsPage;
