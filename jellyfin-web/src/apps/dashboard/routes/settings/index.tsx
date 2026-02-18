import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import MenuItem from '@mui/material/MenuItem';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useLocalizationOptions } from 'apps/dashboard/features/settings/api/useLocalizationOptions';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import { QUERY_KEY as CONFIGURATION_QUERY_KEY, useConfiguration } from 'hooks/useConfiguration';
import { QUERY_KEY as NAMED_CONFIGURATION_QUERY_KEY, useNamedConfiguration } from 'hooks/useNamedConfiguration';
import globalize from 'lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import React, { useCallback, useEffect, useState } from 'react';
import { type ActionFunctionArgs, Form, useActionData, useNavigation } from 'react-router-dom';
import SearchIcon from '@mui/icons-material/Search';
import InputAdornment from '@mui/material/InputAdornment';
import FormControl from '@mui/material/FormControl';
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import DirectoryBrowser from 'components/directorybrowser/directorybrowser';
import { getConfigurationApi } from '@jellyfin/sdk/lib/utils/api/configuration-api';
import { queryClient } from 'utils/query/queryClient';
import { ActionData } from 'types/actionData';
import {
    DEFAULT_SUBSCRIPTION_PRICING,
    normalizeSubscriptionPricing,
    SUBSCRIPTION_CONFIG_KEY,
    SubscriptionPricing
} from 'utils/subscription';

type SettingsActionData = ActionData & {
    error?: string
};

const SUBSCRIPTION_PRICE_FIELDS: Array<{
    name: keyof SubscriptionPricing
    label: string
}> = [
    { name: 'OneMonthPrice', label: '1 Month Price (Rs)' },
    { name: 'ThreeMonthPrice', label: '3 Months Price (Rs)' },
    { name: 'SixMonthPrice', label: '6 Months Price (Rs)' },
    { name: 'TwelveMonthPrice', label: '12 Months Price (Rs)' }
];

export const action = async ({ request }: ActionFunctionArgs) => {
    const api = ServerConnections.getCurrentApi();
    if (!api) throw new Error('No Api instance available');

    const { data: config } = await getConfigurationApi(api).getConfiguration();
    const formData = await request.formData();
    const parseSubscriptionPrice = (fieldName: keyof SubscriptionPricing) => {
        const rawValue = formData.get(fieldName)?.toString().trim() ?? '';
        if (!/^\d+$/.test(rawValue)) {
            throw new Error('Subscription prices must be positive whole numbers.');
        }

        const parsedValue = Number(rawValue);
        if (!Number.isInteger(parsedValue) || parsedValue <= 0) {
            throw new Error('Subscription prices must be positive whole numbers.');
        }

        return parsedValue;
    };

    let subscriptionPricing: SubscriptionPricing;
    try {
        subscriptionPricing = {
            OneMonthPrice: parseSubscriptionPrice('OneMonthPrice'),
            ThreeMonthPrice: parseSubscriptionPrice('ThreeMonthPrice'),
            SixMonthPrice: parseSubscriptionPrice('SixMonthPrice'),
            TwelveMonthPrice: parseSubscriptionPrice('TwelveMonthPrice')
        };
    } catch (error) {
        return {
            isSaved: false,
            error: error instanceof Error ? error.message : 'Invalid subscription pricing values.'
        };
    }

    config.ServerName = formData.get('ServerName')?.toString();
    config.UICulture = formData.get('UICulture')?.toString();
    config.CachePath = formData.get('CachePath')?.toString();
    config.MetadataPath = formData.get('MetadataPath')?.toString();
    config.QuickConnectAvailable = formData.get('QuickConnectAvailable')?.toString() === 'on';
    config.LibraryScanFanoutConcurrency = parseInt(formData.get('LibraryScanFanoutConcurrency')?.toString() || '0', 10);
    config.ParallelImageEncodingLimit = parseInt(formData.get('ParallelImageEncodingLimit')?.toString() || '0', 10);

    await getConfigurationApi(api)
        .updateConfiguration({ serverConfiguration: config });

    await getConfigurationApi(api)
        .updateNamedConfiguration({
            key: SUBSCRIPTION_CONFIG_KEY,
            body: subscriptionPricing
        });

    void queryClient.invalidateQueries({
        queryKey: [ CONFIGURATION_QUERY_KEY ]
    });

    void queryClient.invalidateQueries({
        queryKey: [ NAMED_CONFIGURATION_QUERY_KEY, SUBSCRIPTION_CONFIG_KEY ]
    });

    return {
        isSaved: true
    };
};

export const Component = () => {
    const {
        data: config,
        isPending: isConfigPending,
        isError: isConfigError
    } = useConfiguration();
    const {
        data: languageOptions,
        isPending: isLocalizationOptionsPending,
        isError: isLocalizationOptionsError
    } = useLocalizationOptions();
    const {
        data: subscriptionPricingData,
        isPending: isSubscriptionPricingPending,
        isError: isSubscriptionPricingError
    } = useNamedConfiguration<Partial<SubscriptionPricing>>(SUBSCRIPTION_CONFIG_KEY);

    const navigation = useNavigation();
    const actionData = useActionData() as SettingsActionData | undefined;
    const isSubmitting = navigation.state === 'submitting';
    const [ cachePath, setCachePath ] = useState<string | null | undefined>('');
    const [ metadataPath, setMetadataPath ] = useState<string | null | undefined>('');
    const subscriptionPricing = normalizeSubscriptionPricing(subscriptionPricingData || DEFAULT_SUBSCRIPTION_PRICING);

    const onCachePathChange = useCallback((event: React.ChangeEvent<HTMLTextAreaElement | HTMLInputElement>) => {
        setCachePath(event.target.value);
    }, []);

    const onMetadataPathChange = useCallback((event: React.ChangeEvent<HTMLTextAreaElement | HTMLInputElement>) => {
        setMetadataPath(event.target.value);
    }, []);

    const showCachePathPicker = useCallback(() => {
        const picker = new DirectoryBrowser();

        picker.show({
            callback: function (path: string) {
                if (path) {
                    setCachePath(path);
                }

                picker.close();
            },
            validateWriteable: true,
            header: globalize.translate('HeaderSelectServerCachePath'),
            instruction: globalize.translate('HeaderSelectServerCachePathHelp')
        });
    }, []);

    const showMetadataPathPicker = useCallback(() => {
        const picker = new DirectoryBrowser();

        picker.show({
            path: metadataPath,
            callback: function (path: string) {
                if (path) {
                    setMetadataPath(path);
                }

                picker.close();
            },
            validateWriteable: true,
            header: globalize.translate('HeaderSelectMetadataPath'),
            instruction: globalize.translate('HeaderSelectMetadataPathHelp')
        });
    }, [metadataPath]);

    useEffect(() => {
        if (!isConfigPending && !isConfigError) {
            setCachePath(config.CachePath);
            setMetadataPath(config.MetadataPath);
        }
    }, [config, isConfigPending, isConfigError]);

    if (isConfigPending || isLocalizationOptionsPending || isSubscriptionPricingPending) {
        return <Loading />;
    }

    return (
        <Page
            id='dashboardGeneralPage'
            title={globalize.translate('General')}
            className='type-interior mainAnimatedPage'
        >
            <Box className='content-primary'>
                {isConfigError || isLocalizationOptionsError || isSubscriptionPricingError ? (
                    <Alert severity='error'>{globalize.translate('SettingsPageLoadError')}</Alert>
                ) : (
                    <Form method='POST'>
                        <Stack spacing={3}>
                            <Typography variant='h1'>{globalize.translate('Settings')}</Typography>

                            {!isSubmitting && actionData?.isSaved && (
                                <Alert severity='success'>
                                    {globalize.translate('SettingsSaved')}
                                </Alert>
                            )}

                            {!!actionData?.error && (
                                <Alert severity='error'>
                                    {actionData.error}
                                </Alert>
                            )}

                            <TextField
                                name='ServerName'
                                label={globalize.translate('LabelServerName')}
                                helperText={globalize.translate('LabelServerNameHelp')}
                                defaultValue={config.ServerName}
                            />

                            <TextField
                                select
                                name='UICulture'
                                label={globalize.translate('LabelPreferredDisplayLanguage')}
                                helperText={(
                                    <>
                                        <span>{globalize.translate('LabelDisplayLanguageHelp')}</span>
                                        <Link href='https://jellyfin.org/docs/general/contributing/#translating' target='_blank'>
                                            {globalize.translate('LearnHowYouCanContribute')}
                                        </Link>
                                    </>
                                )}
                                defaultValue={config.UICulture}
                                slotProps={{
                                    formHelperText: { component: Stack }
                                }}
                            >
                                {languageOptions.map((language) =>
                                    <MenuItem key={language.Name} value={language.Value || ''}>{language.Name}</MenuItem>
                                )}
                            </TextField>

                            <Typography variant='h2'>{globalize.translate('HeaderPaths')}</Typography>

                            <TextField
                                name='CachePath'
                                label={globalize.translate('LabelCachePath')}
                                helperText={globalize.translate('LabelCachePathHelp')}
                                value={cachePath}
                                onChange={onCachePathChange}
                                slotProps={{
                                    input: {
                                        endAdornment: (
                                            <InputAdornment position='end'>
                                                <IconButton edge='end' onClick={showCachePathPicker}>
                                                    <SearchIcon />
                                                </IconButton>
                                            </InputAdornment>
                                        )
                                    }
                                }}
                            />

                            <TextField
                                name={'MetadataPath'}
                                label={globalize.translate('LabelMetadataPath')}
                                helperText={globalize.translate('LabelMetadataPathHelp')}
                                value={metadataPath}
                                onChange={onMetadataPathChange}
                                slotProps={{
                                    input: {
                                        endAdornment: (
                                            <InputAdornment position='end'>
                                                <IconButton edge='end' onClick={showMetadataPathPicker}>
                                                    <SearchIcon />
                                                </IconButton>
                                            </InputAdornment>
                                        )
                                    }
                                }}
                            />

                            <Typography variant='h2'>{globalize.translate('QuickConnect')}</Typography>

                            <FormControl>
                                <FormControlLabel
                                    control={
                                        <Checkbox
                                            name='QuickConnectAvailable'
                                            defaultChecked={config.QuickConnectAvailable}
                                        />
                                    }
                                    label={globalize.translate('EnableQuickConnect')}
                                />
                            </FormControl>

                            <Typography variant='h2'>{globalize.translate('HeaderPerformance')}</Typography>

                            <TextField
                                name='LibraryScanFanoutConcurrency'
                                type='number'
                                label={globalize.translate('LibraryScanFanoutConcurrency')}
                                helperText={globalize.translate('LibraryScanFanoutConcurrencyHelp')}
                                defaultValue={config.LibraryScanFanoutConcurrency || ''}
                                slotProps={{
                                    htmlInput: {
                                        min: 0,
                                        step: 1
                                    }
                                }}
                            />

                            <TextField
                                name='ParallelImageEncodingLimit'
                                type='number'
                                label={globalize.translate('LabelParallelImageEncodingLimit')}
                                helperText={globalize.translate('LabelParallelImageEncodingLimitHelp')}
                                defaultValue={config.ParallelImageEncodingLimit || ''}
                                slotProps={{
                                    htmlInput: {
                                        min: 0,
                                        step: 1
                                    }
                                }}
                            />

                            <Typography variant='h2'>Subscription Pricing</Typography>

                            {SUBSCRIPTION_PRICE_FIELDS.map(field => (
                                <TextField
                                    key={field.name}
                                    name={field.name}
                                    type='number'
                                    label={field.label}
                                    defaultValue={subscriptionPricing[field.name]}
                                    slotProps={{
                                        htmlInput: {
                                            min: 1,
                                            step: 1
                                        }
                                    }}
                                />
                            ))}

                            <Button type='submit' size='large'>
                                {globalize.translate('Save')}
                            </Button>
                        </Stack>
                    </Form>
                )}
            </Box>
        </Page>
    );
};

Component.displayName = 'SettingsPage';
