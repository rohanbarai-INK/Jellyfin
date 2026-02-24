import Box from '@mui/material/Box';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import React, { useEffect, useState } from 'react';

import { useApi } from 'hooks/useApi';
import { useSystemInfo } from 'hooks/useSystemInfo';
import ListItemLink from 'components/ListItemLink';
import { getStaticLogoUrl, resolveBrandLogoUrl } from 'utils/brandingLogo';


const DrawerHeaderLink = () => {
    const { api } = useApi();
    const { data: systemInfo } = useSystemInfo();
    const [ logoUrl, setLogoUrl ] = useState(getStaticLogoUrl());

    useEffect(() => {
        void resolveBrandLogoUrl(api)
            .then(setLogoUrl);
    }, [ api ]);

    return (
        <ListItemLink to='/'>
            <ListItemIcon sx={{ minWidth: 56 }}>
                <Box
                    component='img'
                    src={logoUrl}
                    sx={{ height: '2.5rem' }}
                />
            </ListItemIcon>
            <ListItemText
                primary={systemInfo?.ServerName || 'Jellyfin'}
                secondary={systemInfo?.Version}
                slotProps={{
                    primary: { variant: 'h6' }
                }}
            />
        </ListItemLink>);
};

export default DrawerHeaderLink;
