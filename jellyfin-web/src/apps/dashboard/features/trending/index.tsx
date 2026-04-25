import React, { type ChangeEvent, type FormEvent, useCallback, useEffect, useMemo, useState } from 'react';

import './trending.scss';

import { ServerConnections } from 'lib/jellyfin-apiclient';
import {
    deleteTrendingPromotion,
    getAdminTrendingPromotions,
    setTrendingPromotionEnabled,
    type AdminTrendingPromotion,
    type UpsertTrendingPromotionPayload,
    upsertTrendingPromotion
} from 'utils/trendingPromotionsApi';
import {
    getTrendingNowSettings,
    setTrendingNowSettings,
    type TrendingAudienceSegment
} from 'utils/trendingNowApi';

type PromotionFormState = {
    id: string;
    promotionId: string;
    itemId: string;
    enabled: boolean;
    startsAtUtc: string;
    endsAtUtc: string;
    pinPosition: string;
    boostAmount: string;
    audienceSegment: TrendingAudienceSegment;
    audienceValue: string;
    labelOverride: string;
    taglineOverride: string;
    artworkVariant: string;
};

type ContentSearchType = 'Movie' | 'Series';
type ContentSearchResult = {
    id: string;
    name: string;
    productionYear: number | null;
    type: ContentSearchType;
};

const AUDIENCE_OPTIONS: Array<{ value: TrendingAudienceSegment; label: string }> = [
    { value: 'AllUsers', label: 'All Users' },
    { value: 'NewOrLowHistory', label: 'New or Low History' },
    { value: 'ReturningUsers', label: 'Returning Users' },
    { value: 'MovieHeavy', label: 'Movie-Heavy Viewers' },
    { value: 'SeriesHeavy', label: 'Series-Heavy Viewers' },
    { value: 'TopGenreMatch', label: 'Top Genre Match' }
];

const PROMOTION_ID_PRESETS = [
    { value: 'featured-launch', label: 'Featured Launch', help: 'Use this for big launch campaigns where one title should dominate visibility.' },
    { value: 'weekend-spotlight', label: 'Weekend Spotlight', help: 'Use this for short weekend boosts with mild editorial promotion.' },
    { value: 'editors-pick', label: "Editor's Pick", help: 'Use this for curated recommendation campaigns without hard forcing every slot.' },
    { value: 'genre-push', label: 'Genre Push', help: 'Use this for audience-targeted campaigns like Action, Thriller, or Kids.' },
    { value: 'custom', label: 'Custom', help: 'Use this when you need a custom naming strategy for reporting or experiments.' }
];

const FIELD_HELP: Record<string, string> = {
    maxSlides: 'Maximum number of Trending slides shown in homepage rail. Higher values show more items.',
    promotionPattern: 'Chooses a ready-made Promotion ID naming pattern. You can still edit Promotion ID manually.',
    promotionId: 'Campaign key used by admins for tracking, editing, and reporting. Keep it readable and unique.',
    content: 'Search and choose the movie or series to promote. The system stores Item ID in the background.',
    enabled: 'Turns this promotion on or off without deleting it.',
    audienceSegment: 'Defines which viewer segment should receive this promotion.',
    audienceValue: 'Used only for certain segments like Top Genre Match. Example: Action.',
    pinPosition: 'Hard-pins content to a slot in the rail (1 means first). Leave blank for score-based order.',
    boostAmount: 'Soft score boost added on top of trending score. Higher values push content up.',
    startsAtUtc: 'Promotion start date and time in UTC. Leave empty to start immediately.',
    endsAtUtc: 'Promotion end date and time in UTC. Leave empty for no automatic end date.',
    labelOverride: 'Overrides the default badge text shown to users on the rail.',
    taglineOverride: 'Overrides short support line shown in the hero rail.',
    artworkVariant: 'Optional future hint to request preferred artwork style (for example poster or backdrop).'
};

const EMPTY_FORM: PromotionFormState = {
    id: '',
    promotionId: '',
    itemId: '',
    enabled: true,
    startsAtUtc: '',
    endsAtUtc: '',
    pinPosition: '',
    boostAmount: '0',
    audienceSegment: 'AllUsers',
    audienceValue: '',
    labelOverride: '',
    taglineOverride: '',
    artworkVariant: ''
};

const toDateInputValue = (value: string | null) => (
    value ? value.slice(0, 16) : ''
);

const toFormState = (promotion: AdminTrendingPromotion): PromotionFormState => ({
    id: promotion.id,
    promotionId: promotion.promotionId,
    itemId: promotion.itemId,
    enabled: promotion.enabled,
    startsAtUtc: toDateInputValue(promotion.startsAtUtc),
    endsAtUtc: toDateInputValue(promotion.endsAtUtc),
    pinPosition: promotion.pinPosition?.toString() || '',
    boostAmount: promotion.boostAmount.toString(),
    audienceSegment: promotion.audienceSegment,
    audienceValue: promotion.audienceValue,
    labelOverride: promotion.labelOverride,
    taglineOverride: promotion.taglineOverride,
    artworkVariant: promotion.artworkVariant
});

const formatDateLabel = (value: string | null) => {
    if (!value) {
        return 'No schedule';
    }

    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString();
};

const toNullableNumber = (value: string): number | null => {
    const normalized = value.trim();
    if (!normalized) {
        return null;
    }

    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
};

const getPromotionPresetValue = (promotionId: string) => {
    const normalized = promotionId.trim().toLowerCase();
    const matched = PROMOTION_ID_PRESETS.find(preset => preset.value !== 'custom' && normalized.startsWith(preset.value));
    return matched ? matched.value : 'custom';
};

const TooltipLabel = ({
    text,
    tooltip
}: {
    text: string;
    tooltip: string;
}) => (
    <span className='trendingAdminFieldHeader'>
        <span>{text}</span>
        <button
            type='button'
            className='trendingAdminTooltip'
            title={tooltip}
            aria-label={`${text} help`}
        >
            i
        </button>
    </span>
);

const TrendingAdmin = () => {
    const [ promotions, setPromotions ] = useState<AdminTrendingPromotion[]>([]);
    const [ selectedPromotionId, setSelectedPromotionId ] = useState('');
    const [ formState, setFormState ] = useState<PromotionFormState>(EMPTY_FORM);
    const [ isLoading, setIsLoading ] = useState(true);
    const [ isSaving, setIsSaving ] = useState(false);
    const [ message, setMessage ] = useState('');
    const [ pageError, setPageError ] = useState('');
    const [ contentSearchTerm, setContentSearchTerm ] = useState('');
    const [ contentSearchType, setContentSearchType ] = useState<ContentSearchType>('Movie');
    const [ contentSearchResults, setContentSearchResults ] = useState<ContentSearchResult[]>([]);
    const [ isContentSearching, setIsContentSearching ] = useState(false);
    const [ selectedContentTitle, setSelectedContentTitle ] = useState('');
    const [ maxSlides, setMaxSlides ] = useState('12');
    const [ isSavingSettings, setIsSavingSettings ] = useState(false);

    const loadPromotions = useCallback(async (preferredPromotionId?: string) => {
        setIsLoading(true);
        setPageError('');

        try {
            const rows = await getAdminTrendingPromotions();
            setPromotions(rows);

            const nextSelectedId = preferredPromotionId
                || selectedPromotionId
                || rows[0]?.id
                || '';
            const nextSelected = rows.find(row => row.id === nextSelectedId);

            setSelectedPromotionId(nextSelected?.id || '');
            setFormState(nextSelected ? toFormState(nextSelected) : EMPTY_FORM);
            setSelectedContentTitle(nextSelected?.itemTitle || '');
            setContentSearchResults([]);
            setContentSearchTerm('');
        } catch (error) {
            console.error('[TrendingAdmin] failed to load promotions', error);
            setPageError('Trending promotions could not be loaded right now.');
        } finally {
            setIsLoading(false);
        }
    }, [ selectedPromotionId ]);

    useEffect(() => {
        void loadPromotions();
    }, [ loadPromotions ]);

    useEffect(() => {
        let isMounted = true;
        const loadSettings = async () => {
            try {
                const settings = await getTrendingNowSettings();
                if (isMounted) {
                    setMaxSlides(String(settings.maxSlides || 12));
                }
            } catch (error) {
                console.error('[TrendingAdmin] failed to load trending settings', error);
            }
        };

        void loadSettings();
        return () => {
            isMounted = false;
        };
    }, []);

    const sortedPromotions = useMemo(() => [ ...promotions ].sort((left, right) => {
        const leftPin = left.pinPosition ?? Number.MAX_SAFE_INTEGER;
        const rightPin = right.pinPosition ?? Number.MAX_SAFE_INTEGER;
        if (leftPin !== rightPin) {
            return leftPin - rightPin;
        }

        if (left.enabled !== right.enabled) {
            return left.enabled ? -1 : 1;
        }

        return right.boostAmount - left.boostAmount;
    }), [ promotions ]);

    const selectedPromotion = useMemo(
        () => promotions.find(promotion => promotion.id === selectedPromotionId) || null,
        [ promotions, selectedPromotionId ]
    );

    const updateField = useCallback((event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
        const { name, value, type } = event.target;
        const checked = 'checked' in event.target ? event.target.checked : false;
        setFormState(current => ({
            ...current,
            [name]: type === 'checkbox' ? checked : value
        }));
    }, []);

    const onPromotionPresetChange = useCallback((event: ChangeEvent<HTMLSelectElement>) => {
        const selectedPreset = event.target.value;
        if (selectedPreset === 'custom') {
            return;
        }

        setFormState(current => {
            const currentPreset = getPromotionPresetValue(current.promotionId);
            const shouldReplace = !current.promotionId.trim() || currentPreset !== 'custom';
            return {
                ...current,
                promotionId: shouldReplace ? selectedPreset : current.promotionId
            };
        });
    }, []);

    const onContentSearch = useCallback(async () => {
        const apiClient = ServerConnections.currentApiClient();
        const currentUserId = apiClient?.getCurrentUserId();
        const normalizedSearchTerm = contentSearchTerm.trim();
        if (!apiClient || !currentUserId || !normalizedSearchTerm) {
            setContentSearchResults([]);
            return;
        }

        setIsContentSearching(true);
        setPageError('');
        try {
            const response = await apiClient.getItems(currentUserId, {
                SearchTerm: normalizedSearchTerm,
                IncludeItemTypes: contentSearchType,
                Recursive: true,
                Limit: 20,
                Fields: 'ProductionYear'
            });

            const rows = (response.Items || [])
                .filter(item => item.Id && item.Name)
                .map(item => ({
                    id: item.Id || '',
                    name: item.Name || '',
                    productionYear: item.ProductionYear || null,
                    type: contentSearchType
                }));

            setContentSearchResults(rows);
        } catch (error) {
            console.error('[TrendingAdmin] content search failed', error);
            setPageError('Content search failed. Please try a different keyword.');
        } finally {
            setIsContentSearching(false);
        }
    }, [ contentSearchTerm, contentSearchType ]);

    const onSelectContent = useCallback((content: ContentSearchResult) => {
        setFormState(current => ({
            ...current,
            itemId: content.id
        }));
        setSelectedContentTitle(content.name);
        setContentSearchResults([]);
        setMessage(`Selected content: ${content.name}`);
    }, []);

    const selectPromotion = useCallback((promotion: AdminTrendingPromotion) => {
        setSelectedPromotionId(promotion.id);
        setFormState(toFormState(promotion));
        setSelectedContentTitle(promotion.itemTitle || '');
        setContentSearchResults([]);
        setMessage('');
        setPageError('');
    }, []);

    const onCreateNew = useCallback(() => {
        setSelectedPromotionId('');
        setFormState(EMPTY_FORM);
        setSelectedContentTitle('');
        setContentSearchResults([]);
        setContentSearchTerm('');
        setMessage('');
        setPageError('');
    }, []);

    const onSave = useCallback(async (event: FormEvent) => {
        event.preventDefault();
        if (!formState.itemId.trim()) {
            setPageError('Please select content from search before saving.');
            return;
        }

        setIsSaving(true);
        setMessage('');
        setPageError('');

        try {
            const payload: UpsertTrendingPromotionPayload = {
                id: formState.id || undefined,
                promotionId: formState.promotionId.trim(),
                itemId: formState.itemId,
                enabled: formState.enabled,
                startsAtUtc: formState.startsAtUtc ? new Date(formState.startsAtUtc).toISOString() : null,
                endsAtUtc: formState.endsAtUtc ? new Date(formState.endsAtUtc).toISOString() : null,
                pinPosition: toNullableNumber(formState.pinPosition),
                boostAmount: Number(formState.boostAmount || 0),
                audienceSegment: formState.audienceSegment,
                audienceValue: formState.audienceValue,
                labelOverride: formState.labelOverride,
                taglineOverride: formState.taglineOverride,
                artworkVariant: formState.artworkVariant
            };

            const updated = await upsertTrendingPromotion(payload);
            setMessage(formState.id ? 'Trending promotion updated.' : 'Trending promotion created.');
            await loadPromotions(updated.id);
        } catch (error) {
            console.error('[TrendingAdmin] failed to save promotion', error);
            setPageError(error instanceof Error ? error.message : 'Trending promotion could not be saved.');
        } finally {
            setIsSaving(false);
        }
    }, [ formState, loadPromotions ]);

    const onToggleEnabled = useCallback(async () => {
        if (!selectedPromotion) {
            return;
        }

        setIsSaving(true);
        setMessage('');
        setPageError('');

        try {
            const updated = await setTrendingPromotionEnabled(selectedPromotion.id, !selectedPromotion.enabled);
            setMessage(updated.enabled ? 'Promotion enabled.' : 'Promotion disabled.');
            await loadPromotions(updated.id);
        } catch (error) {
            console.error('[TrendingAdmin] failed to toggle promotion', error);
            setPageError(error instanceof Error ? error.message : 'Promotion state could not be updated.');
        } finally {
            setIsSaving(false);
        }
    }, [ loadPromotions, selectedPromotion ]);

    const onDelete = useCallback(async () => {
        if (!selectedPromotion) {
            return;
        }

        const confirmed = window.confirm(`Delete promotion "${selectedPromotion.promotionId}"?`);
        if (!confirmed) {
            return;
        }

        setIsSaving(true);
        setMessage('');
        setPageError('');

        try {
            await deleteTrendingPromotion(selectedPromotion.id);
            setMessage('Trending promotion deleted.');
            await loadPromotions();
        } catch (error) {
            console.error('[TrendingAdmin] failed to delete promotion', error);
            setPageError(error instanceof Error ? error.message : 'Trending promotion could not be deleted.');
        } finally {
            setIsSaving(false);
        }
    }, [ loadPromotions, selectedPromotion ]);

    const onSaveRailSettings = useCallback(async () => {
        const parsed = Number(maxSlides);
        if (!Number.isFinite(parsed) || parsed < 1 || parsed > 30) {
            setPageError('Max Slides must be between 1 and 30.');
            return;
        }

        setIsSavingSettings(true);
        setMessage('');
        setPageError('');

        try {
            const updated = await setTrendingNowSettings(Math.round(parsed));
            setMaxSlides(String(updated.maxSlides));
            setMessage('Trending rail slide count updated.');
        } catch (error) {
            console.error('[TrendingAdmin] failed to save trending settings', error);
            setPageError(error instanceof Error ? error.message : 'Trending settings could not be saved.');
        } finally {
            setIsSavingSettings(false);
        }
    }, [ maxSlides ]);

    const previewPrimaryLabel = formState.labelOverride.trim()
        || (formState.pinPosition.trim() ? 'Featured' : Number(formState.boostAmount || 0) > 0 ? "Editor's Pick" : 'Trending Now');
    const previewExplanation = formState.audienceSegment === 'TopGenreMatch' && formState.audienceValue.trim()
        ? `Trending in ${formState.audienceValue.trim()}`
        : formState.audienceSegment === 'MovieHeavy'
            ? 'Featured for movie fans'
            : formState.audienceSegment === 'SeriesHeavy'
                ? 'Featured for series fans'
                : formState.audienceSegment === 'ReturningUsers'
                    ? 'Featured for returning viewers'
                    : formState.audienceSegment === 'NewOrLowHistory'
                        ? 'Featured for new viewers'
                        : 'Featured by KnightFlix';

    return (
        <div className='trendingAdminRoot'>
            <header className='trendingAdminHeader'>
                <div>
                    <h1>Trending Promotions</h1>
                    <p>Manage the admin promotion layer for the homepage Trending Now rail with pins, boosts, schedules, and audience targeting.</p>
                </div>
                <div className='trendingAdminHeaderActions'>
                    <div className='trendingAdminSettingControl'>
                        <TooltipLabel text='Max Slides' tooltip={FIELD_HELP.maxSlides} />
                        <div className='trendingAdminSettingControlRow'>
                            <input
                                type='number'
                                min='1'
                                max='30'
                                value={maxSlides}
                                onChange={event => setMaxSlides(event.target.value)}
                            />
                            <button
                                type='button'
                                onClick={() => void onSaveRailSettings()}
                                disabled={isSaving || isLoading || isSavingSettings}
                            >
                                {isSavingSettings ? 'Saving...' : 'Save'}
                            </button>
                        </div>
                    </div>
                    <button type='button' className='btnRefresh' onClick={() => void loadPromotions()} disabled={isLoading || isSaving}>
                        Refresh
                    </button>
                    <button type='button' className='btnCreate' onClick={onCreateNew} disabled={isSaving}>
                        New Promotion
                    </button>
                </div>
            </header>

            {(message || pageError) && (
                <p className={`trendingAdminMessage${pageError ? ' error' : ''}`}>
                    {pageError || message}
                </p>
            )}

            <div className='trendingAdminLayout'>
                <aside className='trendingAdminListPanel'>
                    <h2>Configured Promotions</h2>
                    {isLoading ? (
                        <p className='trendingAdminMuted'>Loading promotions...</p>
                    ) : !sortedPromotions.length ? (
                        <p className='trendingAdminMuted'>No promotions yet. Create one to start shaping the Trending rail.</p>
                    ) : (
                        <div className='trendingAdminList'>
                            {sortedPromotions.map(promotion => (
                                <button
                                    type='button'
                                    key={promotion.id}
                                    className={`trendingAdminListItem${promotion.id === selectedPromotionId ? ' selected' : ''}`}
                                    onClick={() => selectPromotion(promotion)}
                                >
                                    <div className='trendingAdminListItemTop'>
                                        <strong>{promotion.itemTitle || promotion.promotionId}</strong>
                                        <span className={`trendingAdminStatusBadge ${promotion.enabled ? 'enabled' : 'disabled'}`}>
                                            {promotion.enabled ? 'Enabled' : 'Disabled'}
                                        </span>
                                    </div>
                                    <p>{promotion.promotionId}</p>
                                    <div className='trendingAdminListMeta'>
                                        <span>{promotion.pinPosition ? `Pin ${promotion.pinPosition}` : `Boost ${promotion.boostAmount}`}</span>
                                        <span>{promotion.audienceSegment}</span>
                                    </div>
                                </button>
                            ))}
                        </div>
                    )}
                </aside>

                <section className='trendingAdminFormPanel'>
                    <form className='trendingAdminFormGrid' onSubmit={onSave}>
                        <label>
                            <TooltipLabel text='Promotion Pattern' tooltip={FIELD_HELP.promotionPattern} />
                            <select value={getPromotionPresetValue(formState.promotionId)} onChange={onPromotionPresetChange}>
                                {PROMOTION_ID_PRESETS.map(option => (
                                    <option key={option.value} value={option.value}>
                                        {option.label}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <label className='span-two'>
                            <TooltipLabel text='Promotion ID' tooltip={FIELD_HELP.promotionId} />
                            <input
                                type='text'
                                name='promotionId'
                                value={formState.promotionId}
                                onChange={updateField}
                                placeholder='featured-launch-summer-hero'
                                list='trendingPromotionIdSuggestions'
                                required
                            />
                            <datalist id='trendingPromotionIdSuggestions'>
                                {PROMOTION_ID_PRESETS.filter(option => option.value !== 'custom').map(option => (
                                    <option key={option.value} value={option.value} />
                                ))}
                            </datalist>
                        </label>
                        <label className='span-two'>
                            <TooltipLabel text='Content [Movie/Series]' tooltip={FIELD_HELP.content} />
                            <div className='trendingAdminSearchBox'>
                                <div className='trendingAdminSearchControls'>
                                    <select value={contentSearchType} onChange={event => setContentSearchType(event.target.value as ContentSearchType)}>
                                        <option value='Movie'>Movie</option>
                                        <option value='Series'>Series</option>
                                    </select>
                                    <input
                                        type='text'
                                        value={contentSearchTerm}
                                        onChange={event => setContentSearchTerm(event.target.value)}
                                        onKeyDown={event => {
                                            if (event.key === 'Enter') {
                                                event.preventDefault();
                                                void onContentSearch();
                                            }
                                        }}
                                        placeholder='Search title'
                                    />
                                    <button
                                        type='button'
                                        className='trendingAdminSearchButton'
                                        disabled={isContentSearching}
                                        onClick={() => {
                                            void onContentSearch();
                                        }}
                                    >
                                        {isContentSearching ? 'Searching...' : 'Search'}
                                    </button>
                                </div>
                                <div className='trendingAdminSelectedContent'>
                                    <strong>Selected:</strong> {selectedContentTitle || 'No content selected'} {formState.itemId ? `(${formState.itemId})` : ''}
                                </div>
                                {!!contentSearchResults.length && (
                                    <div className='trendingAdminSearchResults'>
                                        {contentSearchResults.map(result => (
                                            <button
                                                key={result.id}
                                                type='button'
                                                className='trendingAdminSearchResult'
                                                onClick={() => onSelectContent(result)}
                                            >
                                                <span>{result.name}</span>
                                                <span>{result.productionYear || '-'}</span>
                                            </button>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </label>
                        <label className='trendingAdminToggleLabel'>
                            <TooltipLabel text='Enabled' tooltip={FIELD_HELP.enabled} />
                            <input
                                type='checkbox'
                                name='enabled'
                                checked={formState.enabled}
                                onChange={updateField}
                            />
                        </label>
                        <label>
                            <TooltipLabel text='Audience Segment' tooltip={FIELD_HELP.audienceSegment} />
                            <select name='audienceSegment' value={formState.audienceSegment} onChange={updateField}>
                                {AUDIENCE_OPTIONS.map(option => (
                                    <option key={option.value} value={option.value}>{option.label}</option>
                                ))}
                            </select>
                        </label>
                        <label>
                            <TooltipLabel text='Audience Value' tooltip={FIELD_HELP.audienceValue} />
                            <input
                                type='text'
                                name='audienceValue'
                                value={formState.audienceValue}
                                onChange={updateField}
                                placeholder='Genre name for TopGenreMatch'
                            />
                        </label>
                        <label>
                            <TooltipLabel text='Pin Position' tooltip={FIELD_HELP.pinPosition} />
                            <input
                                type='number'
                                min='1'
                                name='pinPosition'
                                value={formState.pinPosition}
                                onChange={updateField}
                                placeholder='Optional'
                            />
                        </label>
                        <label>
                            <TooltipLabel text='Boost Amount' tooltip={FIELD_HELP.boostAmount} />
                            <input
                                type='number'
                                min='0'
                                step='0.1'
                                name='boostAmount'
                                value={formState.boostAmount}
                                onChange={updateField}
                            />
                        </label>
                        <label>
                            <TooltipLabel text='Start UTC' tooltip={FIELD_HELP.startsAtUtc} />
                            <input
                                type='datetime-local'
                                name='startsAtUtc'
                                value={formState.startsAtUtc}
                                onChange={updateField}
                            />
                        </label>
                        <label>
                            <TooltipLabel text='End UTC' tooltip={FIELD_HELP.endsAtUtc} />
                            <input
                                type='datetime-local'
                                name='endsAtUtc'
                                value={formState.endsAtUtc}
                                onChange={updateField}
                            />
                        </label>
                        <label className='span-two'>
                            <TooltipLabel text='Label Override' tooltip={FIELD_HELP.labelOverride} />
                            <input
                                type='text'
                                name='labelOverride'
                                value={formState.labelOverride}
                                onChange={updateField}
                                placeholder="Featured, Hot #1, Editor's Pick"
                            />
                        </label>
                        <label className='span-two'>
                            <TooltipLabel text='Tagline Override' tooltip={FIELD_HELP.taglineOverride} />
                            <textarea
                                name='taglineOverride'
                                value={formState.taglineOverride}
                                onChange={updateField}
                                rows={3}
                                placeholder='Optional hero support copy for the featured slide'
                            />
                        </label>
                        <label className='span-two'>
                            <TooltipLabel text='Artwork Variant' tooltip={FIELD_HELP.artworkVariant} />
                            <input
                                type='text'
                                name='artworkVariant'
                                value={formState.artworkVariant}
                                onChange={updateField}
                                placeholder='Optional variant hint, for example backdrop or poster'
                            />
                        </label>

                        <div className='trendingAdminActionRow span-two'>
                            <button type='submit' disabled={isSaving}>
                                {isSaving ? 'Saving...' : formState.id ? 'Update Promotion' : 'Create Promotion'}
                            </button>
                            <button type='button' onClick={onToggleEnabled} disabled={!selectedPromotion || isSaving}>
                                {selectedPromotion?.enabled ? 'Disable' : 'Enable'}
                            </button>
                            <button type='button' className='danger' onClick={onDelete} disabled={!selectedPromotion || isSaving}>
                                Delete
                            </button>
                        </div>
                    </form>

                    <div className='trendingAdminPreview'>
                        <h3>Effective Rail Preview</h3>
                        <div className='trendingAdminPreviewCard'>
                            <span className='trendingAdminPreviewBadge'>{previewPrimaryLabel}</span>
                            <strong>{selectedPromotion?.itemTitle || selectedContentTitle || 'Selected title will appear here'}</strong>
                            <p>{previewExplanation}</p>
                            <div className='trendingAdminPreviewMeta'>
                                <span>{formState.pinPosition.trim() ? `Pinned at ${formState.pinPosition}` : `Boost ${formState.boostAmount || '0'}`}</span>
                                <span>{AUDIENCE_OPTIONS.find(option => option.value === formState.audienceSegment)?.label || 'All Users'}</span>
                            </div>
                        </div>
                    </div>

                    <div className='trendingAdminAudit'>
                        <h3>Audit</h3>
                        <p><strong>Created:</strong> {formatDateLabel(selectedPromotion?.createdAtUtc || null)} by {selectedPromotion?.createdByUsername || 'N/A'}</p>
                        <p><strong>Updated:</strong> {formatDateLabel(selectedPromotion?.updatedAtUtc || null)} by {selectedPromotion?.updatedByUsername || 'N/A'}</p>
                    </div>
                </section>
            </div>
        </div>
    );
};

export default TrendingAdmin;
