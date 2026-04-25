import Dashboard from '@mui/icons-material/Dashboard';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';
import LibraryAdd from '@mui/icons-material/LibraryAdd';
import LiveHelp from '@mui/icons-material/LiveHelp';
import Palette from '@mui/icons-material/Palette';
import People from '@mui/icons-material/People';
import PlayCircle from '@mui/icons-material/PlayCircle';
import Settings from '@mui/icons-material/Settings';
import WorkspacePremium from '@mui/icons-material/WorkspacePremium';
import Campaign from '@mui/icons-material/Campaign';
import LocalFireDepartment from '@mui/icons-material/LocalFireDepartment';
import Collapse from '@mui/material/Collapse';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import ListSubheader from '@mui/material/ListSubheader';
import React, { type MouseEvent, useCallback, useState } from 'react';
import { useLocation } from 'react-router-dom';

import ListItemLink from 'components/ListItemLink';
import globalize from 'lib/globalize';
import requestBadgeGif from 'assets/branding/admin-request-badge.gif';
import { useAdminUnseenPendingCount } from 'apps/dashboard/features/contentRequests/api/useAdminUnseenPendingCount';

const LIBRARY_PATHS = [
    '/dashboard/libraries',
    '/dashboard/libraries/display',
    '/dashboard/libraries/metadata',
    '/dashboard/libraries/nfo'
];

const PLAYBACK_PATHS = [
    '/dashboard/playback/transcoding',
    '/dashboard/playback/resume',
    '/dashboard/playback/streaming',
    '/dashboard/playback/trickplay'
];

const ServerDrawerSection = () => {
    const location = useLocation();
    const { data: unseenPendingCount = 0 } = useAdminUnseenPendingCount();

    const [ isLibrarySectionOpen, setIsLibrarySectionOpen ] = useState(LIBRARY_PATHS.includes(location.pathname));
    const [ isPlaybackSectionOpen, setIsPlaybackSectionOpen ] = useState(PLAYBACK_PATHS.includes(location.pathname));

    const onLibrarySectionClick = useCallback((e: MouseEvent) => {
        e.preventDefault();
        e.stopPropagation();
        setIsLibrarySectionOpen(isOpen => !isOpen);
    }, []);

    const onPlaybackSectionClick = useCallback((e: MouseEvent) => {
        e.preventDefault();
        e.stopPropagation();
        setIsPlaybackSectionOpen(isOpen => !isOpen);
    }, []);

    return (
        <List
            aria-labelledby='server-subheader'
            subheader={
                <ListSubheader component='div' id='server-subheader'>
                    {globalize.translate('TabServer')}
                </ListSubheader>
            }
        >
            <ListItem disablePadding>
                <ListItemLink to='/dashboard'>
                    <ListItemIcon>
                        <Dashboard />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('TabDashboard')} />
                </ListItemLink>
            </ListItem>
            <ListItem disablePadding>
                <ListItemLink to='/dashboard/settings'>
                    <ListItemIcon>
                        <Settings />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('General')} />
                </ListItemLink>
            </ListItem>
            <ListItemLink to='/dashboard/branding'>
                <ListItemIcon>
                    <Palette />
                </ListItemIcon>
                <ListItemText primary={globalize.translate('HeaderBranding')} />
            </ListItemLink>
            <ListItem disablePadding>
                <ListItemLink to='/dashboard/users'>
                    <ListItemIcon>
                        <People />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('HeaderUsers')} />
                </ListItemLink>
            </ListItem>
            <ListItem disablePadding>
                <ListItemLink to='/dashboard/requests'>
                    <ListItemIcon>
                        <LiveHelp />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('RequestMenuLabel')} />
                    {unseenPendingCount > 0 && (
                        <img
                            src={requestBadgeGif}
                            alt=''
                            aria-hidden='true'
                            role='presentation'
                            tabIndex={-1}
                            style={{
                                height: '1.8rem',
                                maxHeight: '1.8rem',
                                pointerEvents: 'none'
                            }}
                        />
                    )}
                </ListItemLink>
            </ListItem>
            <ListItem disablePadding>
                <ListItemLink to='/dashboard/subscription-command-center'>
                    <ListItemIcon>
                        <WorkspacePremium />
                    </ListItemIcon>
                    <ListItemText primary='Subscription Command Center' />
                </ListItemLink>
            </ListItem>
            <ListItem disablePadding>
                <ListItemLink to='/dashboard/announcement'>
                    <ListItemIcon>
                        <Campaign />
                    </ListItemIcon>
                    <ListItemText primary='Announcement' />
                </ListItemLink>
            </ListItem>
            <ListItem disablePadding>
                <ListItemLink to='/dashboard/trending'>
                    <ListItemIcon>
                        <LocalFireDepartment />
                    </ListItemIcon>
                    <ListItemText primary='Trending Promotions' />
                </ListItemLink>
            </ListItem>
            <ListItem disablePadding>
                <ListItemButton onClick={onLibrarySectionClick}>
                    <ListItemIcon>
                        <LibraryAdd />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('HeaderLibraries')} />
                    {isLibrarySectionOpen ? <ExpandLess /> : <ExpandMore />}
                </ListItemButton>
            </ListItem>
            <Collapse in={isLibrarySectionOpen} timeout='auto' unmountOnExit>
                <List component='div' disablePadding>
                    <ListItemLink to='/dashboard/libraries' sx={{ pl: 4 }}>
                        <ListItemText inset primary={globalize.translate('HeaderLibraries')} />
                    </ListItemLink>
                    <ListItemLink to='/dashboard/libraries/display' sx={{ pl: 4 }}>
                        <ListItemText inset primary={globalize.translate('Display')} />
                    </ListItemLink>
                    <ListItemLink to='/dashboard/libraries/metadata' sx={{ pl: 4 }}>
                        <ListItemText inset primary={globalize.translate('LabelMetadata')} />
                    </ListItemLink>
                    <ListItemLink to='/dashboard/libraries/nfo' sx={{ pl: 4 }}>
                        <ListItemText inset primary={globalize.translate('TabNfoSettings')} />
                    </ListItemLink>
                </List>
            </Collapse>
            <ListItem disablePadding>
                <ListItemButton onClick={onPlaybackSectionClick}>
                    <ListItemIcon>
                        <PlayCircle />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('TitlePlayback')} />
                    {isPlaybackSectionOpen ? <ExpandLess /> : <ExpandMore />}
                </ListItemButton>
            </ListItem>
            <Collapse in={isPlaybackSectionOpen} timeout='auto' unmountOnExit>
                <List component='div' disablePadding>
                    <ListItemLink to='/dashboard/playback/transcoding' sx={{ pl: 4 }}>
                        <ListItemText inset primary={globalize.translate('Transcoding')} />
                    </ListItemLink>
                    <ListItemLink to='/dashboard/playback/resume' sx={{ pl: 4 }}>
                        <ListItemText inset primary={globalize.translate('ButtonResume')} />
                    </ListItemLink>
                    <ListItemLink to='/dashboard/playback/streaming' sx={{ pl: 4 }}>
                        <ListItemText inset primary={globalize.translate('TabStreaming')} />
                    </ListItemLink>
                    <ListItemLink to='/dashboard/playback/trickplay' sx={{ pl: 4 }}>
                        <ListItemText inset primary={globalize.translate('Trickplay')} />
                    </ListItemLink>
                </List>
            </Collapse>
        </List>
    );
};

export default ServerDrawerSection;
