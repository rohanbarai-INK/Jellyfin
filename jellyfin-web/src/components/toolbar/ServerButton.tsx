import Button from '@mui/material/Button/Button';
import React, { FC, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from 'hooks/useApi';
import { useSystemInfo } from 'hooks/useSystemInfo';
import { getStaticLogoUrl, resolveBrandLogoUrl } from 'utils/brandingLogo';

const ServerButton: FC = () => {
    const { api } = useApi();
    const {
        data: systemInfo,
        isPending
    } = useSystemInfo();
    const [ logoUrl, setLogoUrl ] = useState(getStaticLogoUrl());

    useEffect(() => {
        void resolveBrandLogoUrl(api)
            .then(setLogoUrl);
    }, [ api ]);

    return (
        <Button
            variant='text'
            size='large'
            color='inherit'
            startIcon={
                <img
                    src={logoUrl}
                    alt=''
                    aria-hidden
                    style={{
                        maxHeight: '1.25em',
                        maxWidth: '1.25em'
                    }}
                />
            }
            component={Link}
            to='/'
        >
            {isPending ? '' : (systemInfo?.ServerName || 'Jellyfin')}
        </Button>
    );
};

export default ServerButton;
