import React, { useCallback, useEffect, useRef, useState } from 'react';
import globalize from '../../../../lib/globalize';
import confirm from '../../../../components/confirm/confirm';
import UserCardBox from '../../../../components/dashboard/users/UserCardBox';
import SectionTitleContainer from '../../../../elements/SectionTitleContainer';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import Typography from '@mui/material/Typography';
import '../../../../elements/emby-button/emby-button';
import '../../../../elements/emby-button/paper-icon-button-light';
import '../../../../components/cardbuilder/card.scss';
import '../../../../components/indicators/indicators.scss';
import '../../../../styles/flexstyles.scss';
import Page from '../../../../components/Page';
import { useLocation, useNavigate } from 'react-router-dom';
import Toast from 'apps/dashboard/components/Toast';
import { useUsers } from 'hooks/useUsers';
import Loading from 'components/loading/LoadingComponent';
import { useDeleteUser } from 'apps/dashboard/features/users/api/useDeleteUser';
import dom from 'utils/dom';
import { ServerConnections } from 'lib/jellyfin-apiclient';

type MenuEntry = {
    name?: string;
    id?: string;
    icon?: string;
};

const durationOptions = [ 1, 3, 6, 12 ];

const getServerErrorMessage = async (err: unknown): Promise<string | undefined> => {
    if (err instanceof Response) {
        if (err.status === 401) {
            return 'Unauthorized request. Sign out and sign in again, then retry.';
        }

        if (err.status === 404) {
            return 'Generate endpoint not found. Make sure the backend is running the new access key code.';
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
        return 'Generate endpoint not found. Make sure the backend is running the new access key code.';
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

const UserProfiles = () => {
    const location = useLocation();
    const [ isSettingsSavedToastOpen, setIsSettingsSavedToastOpen ] = useState(false);
    const [ isGenerateAccessKeyModalOpen, setIsGenerateAccessKeyModalOpen ] = useState(false);
    const [ accessKeyDurationMonths, setAccessKeyDurationMonths ] = useState(1);
    const [ generatedAccessKey, setGeneratedAccessKey ] = useState('');
    const [ selectedUserName, setSelectedUserName ] = useState('');
    const [ isGeneratingAccessKey, setIsGeneratingAccessKey ] = useState(false);
    const [ accessKeyErrorMessage, setAccessKeyErrorMessage ] = useState('');
    const element = useRef<HTMLDivElement>(null);
    const navigate = useNavigate();
    const { data: users, isPending } = useUsers();
    const deleteUser = useDeleteUser();

    const handleToastClose = useCallback(() => {
        setIsSettingsSavedToastOpen(false);
    }, []);

    const onGenerateAccessKey = useCallback(async () => {
        const apiClient = ServerConnections.currentApiClient();
        if (!apiClient) {
            setAccessKeyErrorMessage('Unable to reach server API.');
            return;
        }

        setAccessKeyErrorMessage('');
        setIsGeneratingAccessKey(true);
        try {
            const response = await apiClient.ajax({
                type: 'POST',
                url: apiClient.getUrl('Keys/Generate'),
                data: JSON.stringify({
                    DurationMonths: accessKeyDurationMonths
                }),
                dataType: 'json',
                contentType: 'application/json'
            });

            const responseData = response as { Key?: string; key?: string };
            const newKey = responseData.Key ?? responseData.key;
            if (!newKey) {
                throw new Error('No access key returned by server.');
            }

            setGeneratedAccessKey(newKey);
        } catch (err) {
            console.error('[userprofiles] failed to generate access key', err);
            setAccessKeyErrorMessage((await getServerErrorMessage(err)) || 'Failed to generate access key.');
        } finally {
            setIsGeneratingAccessKey(false);
        }
    }, [ accessKeyDurationMonths ]);

    const closeGenerateAccessKeyModal = useCallback(() => {
        setIsGenerateAccessKeyModalOpen(false);
        setGeneratedAccessKey('');
        setAccessKeyErrorMessage('');
        setAccessKeyDurationMonths(1);
        setSelectedUserName('');
    }, []);

    useEffect(() => {
        const page = element.current;

        if (location.state?.openSavedToast) {
            setIsSettingsSavedToastOpen(true);
            window.history.replaceState({}, '');
        }

        if (!page) {
            console.error('Unexpected null reference');
            return;
        }

        const showUserMenu = (elem: HTMLElement) => {
            const card = dom.parentWithClass(elem, 'card');
            const userId = card?.getAttribute('data-userid');
            const username = card?.getAttribute('data-username');

            if (!userId) {
                console.error('Unexpected null user id');
                return;
            }

            const menuItems: MenuEntry[] = [];

            menuItems.push({
                name: globalize.translate('ButtonEditUser'),
                id: 'open',
                icon: 'mode_edit'
            });
            menuItems.push({
                name: globalize.translate('ButtonLibraryAccess'),
                id: 'access',
                icon: 'lock'
            });
            menuItems.push({
                name: globalize.translate('ButtonParentalControl'),
                id: 'parentalcontrol',
                icon: 'person'
            });
            menuItems.push({
                name: 'Generate Access Key',
                id: 'generateaccesskey',
                icon: 'vpn_key'
            });
            menuItems.push({
                name: globalize.translate('Delete'),
                id: 'delete',
                icon: 'delete'
            });

            import('../../../../components/actionSheet/actionSheet').then(({ default: actionsheet }) => {
                actionsheet.show({
                    items: menuItems,
                    positionTo: card,
                    callback: function (id: string) {
                        switch (id) {
                            case 'open':
                                navigate(`/dashboard/users/profile?userId=${userId}`);
                                break;

                            case 'access':
                                navigate(`/dashboard/users/access?userId=${userId}`);
                                break;

                            case 'parentalcontrol':
                                navigate(`/dashboard/users/parentalcontrol?userId=${userId}`);
                                break;

                            case 'generateaccesskey':
                                setGeneratedAccessKey('');
                                setAccessKeyErrorMessage('');
                                setAccessKeyDurationMonths(1);
                                setSelectedUserName(username || '');
                                setIsGenerateAccessKeyModalOpen(true);
                                break;

                            case 'delete':
                                confirmDeleteUser(userId, username);
                        }
                    }
                }).catch(() => {
                    // action sheet closed
                });
            }).catch(err => {
                console.error('[userprofiles] failed to load action sheet', err);
            });
        };

        const confirmDeleteUser = (id: string, username?: string | null) => {
            const title = username ? globalize.translate('DeleteName', username) : globalize.translate('DeleteUser');
            const text = globalize.translate('DeleteUserConfirmation');

            confirm({
                title,
                text,
                confirmText: globalize.translate('Delete'),
                primary: 'delete'
            }).then(function () {
                deleteUser.mutate({
                    userId: id
                });
            }).catch(() => {
                // confirm dialog closed
            });
        };

        const onPageClick = function (e: MouseEvent) {
            const btnUserMenu = dom.parentWithClass(e.target as HTMLElement, 'btnUserMenu');

            if (btnUserMenu) {
                showUserMenu(btnUserMenu);
            }
        };

        const onAddUserClick = function() {
            navigate('/dashboard/users/add');
        };

        page.addEventListener('click', onPageClick);
        (page.querySelector('#btnAddUser') as HTMLButtonElement).addEventListener('click', onAddUserClick);

        return () => {
            page.removeEventListener('click', onPageClick);
            (page.querySelector('#btnAddUser') as HTMLButtonElement).removeEventListener('click', onAddUserClick);
        };
    }, [navigate, deleteUser, location.state?.openSavedToast]);

    if (isPending) {
        return <Loading />;
    }

    return (
        <Page
            id='userProfilesPage'
            className='mainAnimatedPage type-interior userProfilesPage fullWidthContent'
            title={globalize.translate('HeaderUsers')}
        >
            <Toast
                open={isSettingsSavedToastOpen}
                onClose={handleToastClose}
                message={globalize.translate('SettingsSaved')}
            />
            <Dialog
                open={isGenerateAccessKeyModalOpen}
                onClose={closeGenerateAccessKeyModal}
                fullWidth
                maxWidth='sm'
            >
                <DialogTitle>Generate Access Key</DialogTitle>
                <DialogContent>
                    {!!selectedUserName && (
                        <Typography sx={{ mb: 2 }}>
                            User: {selectedUserName}
                        </Typography>
                    )}
                    <FormControl fullWidth variant='standard'>
                        <InputLabel id='access-key-duration-select-label'>Duration</InputLabel>
                        <Select
                            labelId='access-key-duration-select-label'
                            value={String(accessKeyDurationMonths)}
                            label='Duration'
                            // eslint-disable-next-line react/jsx-no-bind
                            onChange={event => setAccessKeyDurationMonths(parseInt(event.target.value, 10))}
                        >
                            {durationOptions.map(duration => (
                                <MenuItem key={duration} value={String(duration)}>
                                    {duration} month{duration === 1 ? '' : 's'}
                                </MenuItem>
                            ))}
                        </Select>
                    </FormControl>
                    {!!generatedAccessKey && (
                        <Typography sx={{ mt: 2 }}>
                            Generated Key: <strong>{generatedAccessKey}</strong>
                        </Typography>
                    )}
                    {!!accessKeyErrorMessage && (
                        <Typography sx={{ mt: 2 }} color='error'>
                            {accessKeyErrorMessage}
                        </Typography>
                    )}
                </DialogContent>
                <DialogActions>
                    <Button onClick={closeGenerateAccessKeyModal}>
                        {globalize.translate('ButtonCancel')}
                    </Button>
                    <Button onClick={onGenerateAccessKey} disabled={isGeneratingAccessKey}>
                        {isGeneratingAccessKey ? 'Generating...' : 'Generate'}
                    </Button>
                </DialogActions>
            </Dialog>
            <div ref={element} className='content-primary'>
                <div className='verticalSection'>
                    <SectionTitleContainer
                        title={globalize.translate('HeaderUsers')}
                        isBtnVisible={true}
                        btnId='btnAddUser'
                        btnClassName='fab submit sectionTitleButton'
                        btnTitle='ButtonAddUser'
                        btnIcon='add'
                    />
                </div>

                <div
                    style={{
                        marginBottom: '0.75rem',
                        opacity: 0.8
                    }}
                >
                    Expiry Date is shown on each user card.
                </div>
                <div className='localUsers itemsContainer vertical-wrap'>
                    {users?.map(user => {
                        return <UserCardBox key={user.Id} user={user} />;
                    })}
                </div>
            </div>
        </Page>

    );
};

export default UserProfiles;
