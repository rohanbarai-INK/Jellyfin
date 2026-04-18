import React, { useMemo, type FC } from 'react';
import { useSearchParams } from 'react-router-dom';

import Page from 'components/Page';
import { useApi } from 'hooks/useApi';
import { useUsers } from 'hooks/useUsers';

import LeaderboardTab from './LeaderboardTab';

const LeaderboardPage: FC = () => {
    const { user: currentUser } = useApi();
    const { data: users } = useUsers();
    const [ searchParams ] = useSearchParams();

    const requestedUserId = useMemo(() => {
        return searchParams.get('userId') || currentUser?.Id || '';
    }, [ currentUser?.Id, searchParams ]);

    const targetUserName = useMemo(() => {
        if (!requestedUserId) {
            return '';
        }

        if (requestedUserId === currentUser?.Id) {
            return currentUser?.Name || '';
        }

        return users?.find(user => user.Id === requestedUserId)?.Name || '';
    }, [ currentUser?.Id, currentUser?.Name, requestedUserId, users ]);

    return (
        <Page
            id='leaderboardPage'
            className='libraryPage userPreferencesPage noSecondaryNavPage mainAnimatedPage'
            title='Leaderboard'
            shouldAutoFocus
        >
            <div className='padded-left padded-right padded-bottom-page padded-top'>
                <div
                    className='readOnlyContent'
                    style={{
                        margin: '0 auto',
                        maxWidth: '70rem'
                    }}
                >
                    <div className='verticalSection verticalSection-extrabottompadding'>
                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'space-between',
                                gap: '0.8rem',
                                flexWrap: 'wrap'
                            }}
                        >
                            <h2
                                className='sectionTitle'
                                style={{
                                    paddingLeft: '0.25em',
                                    minWidth: 0,
                                    overflowWrap: 'anywhere'
                                }}
                            >
                                {targetUserName ? `${targetUserName} - Leaderboard` : 'Leaderboard'}
                            </h2>
                        </div>

                        <LeaderboardTab />
                    </div>
                </div>
            </div>
        </Page>
    );
};

export default LeaderboardPage;
