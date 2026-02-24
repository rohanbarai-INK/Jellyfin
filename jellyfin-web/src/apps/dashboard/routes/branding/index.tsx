import type { BrandingOptions } from '@jellyfin/sdk/lib/generated-client/models/branding-options';
import { getConfigurationApi } from '@jellyfin/sdk/lib/utils/api/configuration-api';
import { getImageApi } from '@jellyfin/sdk/lib/utils/api/image-api';
import Delete from '@mui/icons-material/Delete';
import Upload from '@mui/icons-material/Upload';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import FormControlLabel from '@mui/material/FormControlLabel';
import Stack from '@mui/material/Stack';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import React, { useCallback, useEffect, useState } from 'react';
import { type ActionFunctionArgs, Form, useActionData, useNavigation } from 'react-router-dom';

import { getBrandingOptionsQuery, QUERY_KEY, useBrandingOptions } from 'apps/dashboard/features/branding/api/useBrandingOptions';
import Loading from 'components/loading/LoadingComponent';
import Image from 'components/Image';
import Page from 'components/Page';
import { LOGO_URL, SPLASHSCREEN_URL } from 'constants/branding';
import { useApi } from 'hooks/useApi';
import globalize from 'lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { ActionData } from 'types/actionData';
import { applyBrandLogoCssVariables, getStaticLogoUrl, invalidateBrandLogoCache } from 'utils/brandingLogo';
import { queryClient } from 'utils/query/queryClient';

const BRANDING_CONFIG_KEY = 'branding';
const BrandingOption = {
    CustomCss: 'CustomCss',
    LoginDisclaimer: 'LoginDisclaimer',
    SplashscreenEnabled: 'SplashscreenEnabled',
    LogoEnabled: 'LogoEnabled'
};

export const action = async ({ request }: ActionFunctionArgs) => {
    const api = ServerConnections.getCurrentApi();
    if (!api) throw new Error('No Api instance available');

    const formData = await request.formData();
    const data = Object.fromEntries(formData);

    const brandingOptions: BrandingOptions & { LogoEnabled: boolean } = {
        CustomCss: data.CustomCss?.toString(),
        LoginDisclaimer: data.LoginDisclaimer?.toString(),
        SplashscreenEnabled: data.SplashscreenEnabled?.toString() === 'on',
        LogoEnabled: data.LogoEnabled?.toString() === 'on'
    };

    await getConfigurationApi(api)
        .updateNamedConfiguration({
            key: BRANDING_CONFIG_KEY,
            body: JSON.stringify(brandingOptions)
        });

    invalidateBrandLogoCache(api);
    void applyBrandLogoCssVariables(api, true);

    void queryClient.invalidateQueries({
        queryKey: [ QUERY_KEY ]
    });

    return {
        isSaved: true
    };
};

export const loader = () => {
    return queryClient.ensureQueryData(
        getBrandingOptionsQuery(ServerConnections.getCurrentApi()));
};

export const Component = () => {
    const { api } = useApi();
    const navigation = useNavigation();
    const actionData = useActionData() as ActionData | undefined;
    const isSubmitting = navigation.state === 'submitting';

    const {
        data: defaultBrandingOptions,
        isPending
    } = useBrandingOptions();
    const [ brandingOptions, setBrandingOptions ] = useState<BrandingOptions>(defaultBrandingOptions || {});
    const [ namedBrandingConfig, setNamedBrandingConfig ] = useState<Record<string, unknown>>({});

    const [ error, setError ] = useState<string>();

    const [ isSplashscreenEnabled, setIsSplashscreenEnabled ] = useState(brandingOptions.SplashscreenEnabled ?? false);
    const [ isLogoEnabled, setIsLogoEnabled ] = useState(true);
    const [ splashscreenUrl, setSplashscreenUrl ] = useState<string>();
    const [ logoUrl, setLogoUrl ] = useState<string>(getStaticLogoUrl());

    useEffect(() => {
        if (!defaultBrandingOptions) return;
        setBrandingOptions(defaultBrandingOptions);
        setIsSplashscreenEnabled(defaultBrandingOptions.SplashscreenEnabled ?? false);
    }, [ defaultBrandingOptions ]);

    const refreshLogoPreview = useCallback(async () => {
        if (!api) return;

        const url = api.getUri(LOGO_URL, { t: Date.now() });
        try {
            const response = await fetch(url, {
                method: 'GET',
                credentials: 'include',
                cache: 'no-store'
            });

            if (response.ok) {
                setLogoUrl(url);
                return;
            }
        } catch {
            // fall through to static logo fallback
        }

        setLogoUrl(getStaticLogoUrl());
    }, [ api ]);

    useEffect(() => {
        if (!api || isSubmitting) return;

        setSplashscreenUrl(api.getUri(SPLASHSCREEN_URL, { t: Date.now() }));
        void refreshLogoPreview();
    }, [ api, isSubmitting, refreshLogoPreview ]);

    useEffect(() => {
        if (!api) return;

        getConfigurationApi(api)
            .getNamedConfiguration({ key: BRANDING_CONFIG_KEY })
            .then(({ data }) => {
                const config = (data ?? {}) as unknown as Record<string, unknown>;
                setNamedBrandingConfig(config);
                setIsLogoEnabled(typeof config.LogoEnabled === 'boolean' ? config.LogoEnabled : true);
            })
            .catch(e => {
                console.error('[BrandingPage] failed to load named branding configuration', e);
                setError('ServerUpdateNeeded');
            });
    }, [ api ]);

    const saveNamedBrandingConfig = useCallback(async (patch: Record<string, unknown>) => {
        if (!api) return;

        const nextConfig = {
            ...namedBrandingConfig,
            ...patch
        };

        await getConfigurationApi(api)
            .updateNamedConfiguration({
                key: BRANDING_CONFIG_KEY,
                body: JSON.stringify(nextConfig)
            });

        setNamedBrandingConfig(nextConfig);

        void queryClient.invalidateQueries({
            queryKey: [ QUERY_KEY ]
        });
    }, [ api, namedBrandingConfig ]);

    const onSplashscreenDelete = useCallback(() => {
        setError(undefined);

        if (!api) return;

        getImageApi(api)
            .deleteCustomSplashscreen()
            .then(() => {
                setSplashscreenUrl(api.getUri(SPLASHSCREEN_URL, { t: Date.now() }));
            })
            .catch(e => {
                console.error('[BrandingPage] error deleting image', e);
                setError('ImageDeleteFailed');
            });
    }, [ api ]);

    const onSplashscreenUpload = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setError(undefined);

        const files = event.target.files;

        if (!api || !files) return false;

        const file = files[0];
        const reader = new FileReader();
        reader.onerror = e => {
            console.error('[BrandingPage] error reading file', e);
            setError('ImageUploadFailed');
        };
        reader.onabort = e => {
            console.warn('[BrandingPage] aborted reading file', e);
            setError('ImageUploadCancelled');
        };
        reader.onload = () => {
            if (!reader.result) return;

            const dataUrl = reader.result as string; // readAsDataURL produces a string
            // FIXME: TypeScript SDK thinks body should be a File but in reality it is a Base64 string
            const body = dataUrl.split(',')[1] as never;
            getImageApi(api)
                .uploadCustomSplashscreen(
                    { body },
                    { headers: { ['Content-Type']: file.type } }
                )
                .then(() => {
                    setSplashscreenUrl(dataUrl);
                })
                .catch(e => {
                    console.error('[BrandingPage] error uploading splashscreen', e);
                    setError('ImageUploadFailed');
                });
        };

        reader.readAsDataURL(file);
    }, [ api ]);

    const onLogoDelete = useCallback(() => {
        setError(undefined);

        if (!api) return;

        api.axiosInstance
            .delete(LOGO_URL)
            .then(async () => {
                invalidateBrandLogoCache(api);
                await applyBrandLogoCssVariables(api, true);
                await refreshLogoPreview();
            })
            .catch(e => {
                console.error('[BrandingPage] error deleting logo', e);
                setError('ImageDeleteFailed');
            });
    }, [ api, refreshLogoPreview ]);

    const onLogoUpload = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setError(undefined);

        const files = event.target.files;

        if (!api || !files) return false;

        const file = files[0];
        const reader = new FileReader();
        reader.onerror = e => {
            console.error('[BrandingPage] error reading logo file', e);
            setError('ImageUploadFailed');
        };
        reader.onabort = e => {
            console.warn('[BrandingPage] aborted reading logo file', e);
            setError('ImageUploadCancelled');
        };
        reader.onload = () => {
            if (!reader.result) return;

            const dataUrl = reader.result as string;
            const body = dataUrl.split(',')[1];

            api.axiosInstance
                .post(LOGO_URL, body, { headers: { ['Content-Type']: file.type } })
                .then(async () => {
                    invalidateBrandLogoCache(api);
                    await applyBrandLogoCssVariables(api, true);
                    await refreshLogoPreview();
                })
                .catch(e => {
                    console.error('[BrandingPage] error uploading logo', e);
                    setError('ImageUploadFailed');
                });
        };

        reader.readAsDataURL(file);
    }, [ api, refreshLogoPreview ]);

    const setSplashscreenEnabled = useCallback(async (_: React.ChangeEvent<HTMLInputElement>, isEnabled: boolean) => {
        setIsSplashscreenEnabled(isEnabled);

        await saveNamedBrandingConfig({
            SplashscreenEnabled: isEnabled
        });
    }, [ saveNamedBrandingConfig ]);

    const setLogoEnabled = useCallback(async (_: React.ChangeEvent<HTMLInputElement>, isEnabled: boolean) => {
        setIsLogoEnabled(isEnabled);

        await saveNamedBrandingConfig({
            LogoEnabled: isEnabled
        });

        if (api) {
            invalidateBrandLogoCache(api);
            await applyBrandLogoCssVariables(api, true);
            await refreshLogoPreview();
        }
    }, [ api, refreshLogoPreview, saveNamedBrandingConfig ]);

    const setBrandingOption = useCallback((event: React.ChangeEvent<HTMLTextAreaElement | HTMLInputElement>) => {
        if (Object.keys(BrandingOption).includes(event.target.name)) {
            setBrandingOptions({
                ...brandingOptions,
                [event.target.name]: event.target.value
            });
        }
    }, [ brandingOptions ]);

    const onSubmit = useCallback(() => {
        setError(undefined);
    }, []);

    if (isPending) return <Loading />;

    return (
        <Page
            id='brandingPage'
            title={globalize.translate('HeaderBranding')}
            className='mainAnimatedPage type-interior'
        >
            <Box className='content-primary'>
                <Form
                    method='POST'
                    onSubmit={onSubmit}
                >
                    <Stack spacing={3}>
                        <Typography variant='h1'>
                            {globalize.translate('HeaderBranding')}
                        </Typography>

                        {!isSubmitting && actionData?.isSaved && (
                            <Alert severity='success'>
                                {globalize.translate('SettingsSaved')}
                            </Alert>
                        )}

                        {error && (
                            <Alert severity='error'>
                                {globalize.translate(error)}
                            </Alert>
                        )}

                        <Typography variant='h2'>
                            {globalize.translate('Logo')}
                        </Typography>

                        <Stack
                            direction={{
                                xs: 'column',
                                sm: 'row'
                            }}
                            spacing={3}
                        >
                            <Box sx={{ flex: '1 1 0' }}>
                                <Image
                                    isLoading={false}
                                    url={logoUrl}
                                />
                            </Box>

                            <Stack
                                spacing={{ xs: 3, sm: 2 }}
                                sx={{ flex: '1 1 0' }}
                            >
                                <FormControlLabel
                                    control={
                                        <Switch
                                            name={BrandingOption.LogoEnabled}
                                            checked={isLogoEnabled}
                                            onChange={setLogoEnabled}
                                        />
                                    }
                                    label={globalize.translate('Logo')}
                                />

                                <Button
                                    component='label'
                                    variant='outlined'
                                    startIcon={<Upload />}
                                >
                                    <input
                                        type='file'
                                        accept='image/png,image/jpeg,image/webp'
                                        hidden
                                        onChange={onLogoUpload}
                                    />
                                    {globalize.translate('UploadCustomImage')}
                                </Button>

                                <Button
                                    variant='outlined'
                                    color='error'
                                    startIcon={<Delete />}
                                    onClick={onLogoDelete}
                                >
                                    {globalize.translate('DeleteCustomImage')}
                                </Button>
                            </Stack>
                        </Stack>

                        <Typography variant='h2'>
                            {globalize.translate('EnableSplashScreen')}
                        </Typography>

                        <Stack
                            direction={{
                                xs: 'column',
                                sm: 'row'
                            }}
                            spacing={3}
                        >
                            <Box sx={{ flex: '1 1 0' }}>
                                <Image
                                    isLoading={false}
                                    url={
                                        isSplashscreenEnabled ?
                                            splashscreenUrl :
                                            undefined
                                    }
                                />
                            </Box>

                            <Stack
                                spacing={{ xs: 3, sm: 2 }}
                                sx={{ flex: '1 1 0' }}
                            >
                                <FormControlLabel
                                    control={
                                        <Switch
                                            name={BrandingOption.SplashscreenEnabled}
                                            checked={isSplashscreenEnabled}
                                            onChange={setSplashscreenEnabled}
                                        />
                                    }
                                    label={globalize.translate('EnableSplashScreen')}
                                />

                                <Typography variant='body2'>
                                    {globalize.translate('CustomSplashScreenSize')}
                                </Typography>

                                <Button
                                    component='label'
                                    variant='outlined'
                                    startIcon={<Upload />}
                                    disabled={!isSplashscreenEnabled}
                                >
                                    <input
                                        type='file'
                                        accept='image/*'
                                        hidden
                                        onChange={onSplashscreenUpload}
                                    />
                                    {globalize.translate('UploadCustomImage')}
                                </Button>

                                <Button
                                    variant='outlined'
                                    color='error'
                                    startIcon={<Delete />}
                                    disabled={!isSplashscreenEnabled}
                                    onClick={onSplashscreenDelete}
                                >
                                    {globalize.translate('DeleteCustomImage')}
                                </Button>
                            </Stack>
                        </Stack>

                        <TextField
                            fullWidth
                            multiline
                            minRows={5}
                            maxRows={5}
                            name={BrandingOption.LoginDisclaimer}
                            label={globalize.translate('LabelLoginDisclaimer')}
                            helperText={globalize.translate('LabelLoginDisclaimerHelp')}
                            value={brandingOptions?.LoginDisclaimer}
                            onChange={setBrandingOption}
                            slotProps={{
                                input: {
                                    className: 'textarea-mono'
                                }
                            }}
                        />

                        <TextField
                            fullWidth
                            multiline
                            minRows={5}
                            maxRows={20}
                            name={BrandingOption.CustomCss}
                            label={globalize.translate('LabelCustomCss')}
                            helperText={globalize.translate('LabelCustomCssHelp')}
                            spellCheck={false}
                            value={brandingOptions?.CustomCss}
                            onChange={setBrandingOption}
                            slotProps={{
                                input: {
                                    className: 'textarea-mono'
                                }
                            }}
                        />

                        <Button
                            type='submit'
                            size='large'
                        >
                            {globalize.translate('Save')}
                        </Button>
                    </Stack>
                </Form>
            </Box>
        </Page>
    );
};

Component.displayName = 'BrandingPage';
