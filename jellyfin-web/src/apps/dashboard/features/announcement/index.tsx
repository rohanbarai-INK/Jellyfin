import React, { useCallback, useEffect, useMemo, useState } from 'react';

import FeatureAnnouncementPopup from 'components/featureAnnouncements/FeatureAnnouncementPopup';
import type { FeatureAnnouncementCampaign } from 'components/featureAnnouncements/featureAnnouncementTypes';
import { useApi } from 'hooks/useApi';
import {
    type AdminFeatureAnnouncement,
    getAdminFeatureAnnouncements,
    toFeatureAnnouncementCampaignForPreview,
    upsertAdminFeatureAnnouncement
} from 'utils/featureAnnouncementsApi';

import './announcement.scss';

type AnnouncementStatus = 'Draft' | 'Published';
type CtaTargetType = 'InternalRoute' | 'ExternalUrl';

interface AnnouncementFormState {
    id: string
    campaignId: string
    enabled: boolean
    status: AnnouncementStatus
    heading: string
    title: string
    subtitle: string
    description: string
    highlightsText: string
    helpText: string
    heroGifSource: string
    mediaImageSource: string
    mediaImageAlt: string
    mediaImageCaption: string
    ctaLabel: string
    ctaTargetType: CtaTargetType
    ctaTarget: string
    closeLabel: string
    startsAtUtc: string | null
    endsAtUtc: string | null
    maxImpressionsPerDay: number
    maxImpressionsTotal: number
    priority: number
    sortOrder: number
    createdAtUtc: string
    updatedAtUtc: string
    createdByUsername: string
    updatedByUsername: string
}

const DEFAULT_FORM_STATE: AnnouncementFormState = {
    id: '',
    campaignId: '',
    enabled: true,
    status: 'Draft',
    heading: "What's New?",
    title: '',
    subtitle: '',
    description: '',
    highlightsText: '',
    helpText: '',
    heroGifSource: 'builtin:request-popup-accent',
    mediaImageSource: 'builtin:leaderboard-announcement-preview',
    mediaImageAlt: 'Announcement media preview',
    mediaImageCaption: '',
    ctaLabel: 'Check It Out',
    ctaTargetType: 'InternalRoute',
    ctaTarget: '/achievements',
    closeLabel: 'Close',
    startsAtUtc: null,
    endsAtUtc: null,
    maxImpressionsPerDay: 2,
    maxImpressionsTotal: 10,
    priority: 100,
    sortOrder: 100,
    createdAtUtc: '',
    updatedAtUtc: '',
    createdByUsername: '',
    updatedByUsername: ''
};

const toDateTimeLocalValue = (isoValue: string | null) => {
    if (!isoValue) {
        return '';
    }

    const parsed = new Date(isoValue);
    if (Number.isNaN(parsed.getTime())) {
        return '';
    }

    const normalized = new Date(parsed.getTime() - (parsed.getTimezoneOffset() * 60_000));
    return normalized.toISOString().slice(0, 16);
};

const toIsoOrNull = (localDateTimeValue: string) => {
    if (!localDateTimeValue.trim()) {
        return null;
    }

    const parsed = new Date(localDateTimeValue);
    if (Number.isNaN(parsed.getTime())) {
        return null;
    }

    return parsed.toISOString();
};

const toFormState = (announcement: AdminFeatureAnnouncement): AnnouncementFormState => ({
    id: announcement.id,
    campaignId: announcement.campaignId,
    enabled: announcement.enabled,
    status: announcement.status,
    heading: announcement.heading,
    title: announcement.title,
    subtitle: announcement.subtitle,
    description: announcement.description,
    highlightsText: announcement.highlights.join('\n'),
    helpText: announcement.helpText,
    heroGifSource: announcement.heroGifSource,
    mediaImageSource: announcement.mediaImageSource,
    mediaImageAlt: announcement.mediaImageAlt,
    mediaImageCaption: announcement.mediaImageCaption,
    ctaLabel: announcement.ctaLabel,
    ctaTargetType: announcement.ctaTargetType,
    ctaTarget: announcement.ctaTarget,
    closeLabel: announcement.closeLabel,
    startsAtUtc: announcement.startsAtUtc,
    endsAtUtc: announcement.endsAtUtc,
    maxImpressionsPerDay: announcement.maxImpressionsPerDay,
    maxImpressionsTotal: announcement.maxImpressionsTotal,
    priority: announcement.priority,
    sortOrder: announcement.sortOrder,
    createdAtUtc: announcement.createdAtUtc,
    updatedAtUtc: announcement.updatedAtUtc,
    createdByUsername: announcement.createdByUsername,
    updatedByUsername: announcement.updatedByUsername
});

const parseErrorMessage = async (error: unknown) => {
    if (!error || typeof error !== 'object') {
        return 'Unexpected error occurred.';
    }

    const responseError = error as {
        text?: () => Promise<string>
        response?: {
            data?: unknown
        }
    };

    const responseData = responseError.response?.data;
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

    if (typeof responseError.text === 'function') {
        const text = await responseError.text();
        if (text.trim()) {
            return text;
        }
    }

    return 'Unexpected error occurred.';
};

const readFileAsDataUrl = (file: File) => new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
        if (typeof reader.result === 'string') {
            resolve(reader.result);
            return;
        }

        reject(new Error('Unable to read selected file.'));
    };
    reader.onerror = () => {
        reject(new Error('Unable to read selected file.'));
    };

    reader.readAsDataURL(file);
});

type TextInputField =
    | 'campaignId'
    | 'heading'
    | 'title'
    | 'subtitle'
    | 'description'
    | 'highlightsText'
    | 'helpText'
    | 'heroGifSource'
    | 'mediaImageSource'
    | 'mediaImageAlt'
    | 'mediaImageCaption'
    | 'ctaLabel'
    | 'ctaTarget'
    | 'closeLabel';

type NumberInputField =
    | 'maxImpressionsPerDay'
    | 'maxImpressionsTotal'
    | 'priority'
    | 'sortOrder';

type DateInputField = 'startsAtUtc' | 'endsAtUtc';

const getDateRangeValidationError = (startsAtUtc: string | null, endsAtUtc: string | null) => {
    if (!startsAtUtc || !endsAtUtc) {
        return '';
    }

    const startsAt = new Date(startsAtUtc);
    const endsAt = new Date(endsAtUtc);
    if (Number.isNaN(startsAt.getTime()) || Number.isNaN(endsAt.getTime())) {
        return '';
    }

    return startsAt > endsAt ? 'Start date must be earlier than or equal to end date.' : '';
};

const getCtaValidationError = (ctaTargetType: CtaTargetType, ctaTarget: string) => {
    if (ctaTargetType === 'InternalRoute') {
        return ctaTarget.trim().startsWith('/') ? '' : 'Internal CTA target must begin with /.';
    }

    const normalized = ctaTarget.trim();
    const isHttpUrl = /^https?:\/\/\S+$/i.test(normalized);
    if (isHttpUrl) {
        return '';
    }

    return 'External CTA target must be a valid http/https URL.';
};

const buildValidationErrors = (state: AnnouncementFormState) => {
    const errors: string[] = [];

    if (!state.title.trim()) {
        errors.push('Title is required.');
    }

    if (!state.description.trim()) {
        errors.push('Description is required.');
    }

    if (state.maxImpressionsPerDay <= 0) {
        errors.push('Max impressions per day must be greater than zero.');
    }

    if (state.maxImpressionsTotal <= 0) {
        errors.push('Max total impressions must be greater than zero.');
    }

    const dateRangeError = getDateRangeValidationError(state.startsAtUtc, state.endsAtUtc);
    if (dateRangeError) {
        errors.push(dateRangeError);
    }

    const ctaTargetError = getCtaValidationError(state.ctaTargetType, state.ctaTarget);
    if (ctaTargetError) {
        errors.push(ctaTargetError);
    }

    return errors;
};

const AnnouncementAdmin = () => {
    const { __legacyApiClient__: apiClient } = useApi();

    const [ announcements, setAnnouncements ] = useState<AdminFeatureAnnouncement[]>([]);
    const [ selectedAnnouncementId, setSelectedAnnouncementId ] = useState('');
    const [ formState, setFormState ] = useState<AnnouncementFormState>(DEFAULT_FORM_STATE);
    const [ isLoading, setIsLoading ] = useState(true);
    const [ isSaving, setIsSaving ] = useState(false);
    const [ pageMessage, setPageMessage ] = useState('');
    const [ pageError, setPageError ] = useState(false);
    const [ isPreviewOpen, setIsPreviewOpen ] = useState(false);

    const announcementsById = useMemo(() => {
        const map = new Map<string, AdminFeatureAnnouncement>();
        for (const announcement of announcements) {
            map.set(announcement.id, announcement);
        }

        return map;
    }, [ announcements ]);

    const sortedAnnouncements = useMemo(() => [ ...announcements ]
        .sort((left, right) => {
            const priorityDiff = right.priority - left.priority;
            if (priorityDiff !== 0) {
                return priorityDiff;
            }

            const sortOrderDiff = right.sortOrder - left.sortOrder;
            if (sortOrderDiff !== 0) {
                return sortOrderDiff;
            }

            return new Date(right.updatedAtUtc).getTime() - new Date(left.updatedAtUtc).getTime();
        }), [ announcements ]);

    const refreshAnnouncements = useCallback(async () => {
        setIsLoading(true);
        try {
            const rows = await getAdminFeatureAnnouncements(apiClient || undefined);
            setAnnouncements(rows);

            if (!rows.length) {
                setSelectedAnnouncementId('');
                setFormState(DEFAULT_FORM_STATE);
                return;
            }

            const selected = rows.find(row => row.id === selectedAnnouncementId) || rows[0];
            setSelectedAnnouncementId(selected.id);
            setFormState(toFormState(selected));
        } catch (error) {
            setPageError(true);
            setPageMessage(await parseErrorMessage(error));
        } finally {
            setIsLoading(false);
        }
    }, [ apiClient, selectedAnnouncementId ]);

    useEffect(() => {
        void refreshAnnouncements();
    }, [ refreshAnnouncements ]);

    const onSelectAnnouncement = useCallback((announcement: AdminFeatureAnnouncement) => {
        setSelectedAnnouncementId(announcement.id);
        setFormState(toFormState(announcement));
        setPageMessage('');
        setPageError(false);
    }, []);

    const onCreateAnnouncement = useCallback(() => {
        setSelectedAnnouncementId('');
        setFormState(DEFAULT_FORM_STATE);
        setPageMessage('');
        setPageError(false);
    }, []);

    const onFieldChange = useCallback(<K extends keyof AnnouncementFormState>(field: K, value: AnnouncementFormState[K]) => {
        setFormState(previous => ({
            ...previous,
            [field]: value
        }));
    }, []);

    const onTextInputChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        const fieldName = event.target.name as TextInputField;
        onFieldChange(fieldName, event.target.value as AnnouncementFormState[TextInputField]);
    }, [ onFieldChange ]);

    const onNumberInputChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        const fieldName = event.target.name as NumberInputField;
        onFieldChange(fieldName, (Number(event.target.value) || 0) as AnnouncementFormState[NumberInputField]);
    }, [ onFieldChange ]);

    const onDateInputChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        const fieldName = event.target.name as DateInputField;
        onFieldChange(fieldName, toIsoOrNull(event.target.value) as AnnouncementFormState[DateInputField]);
    }, [ onFieldChange ]);

    const onStatusChange = useCallback((event: React.ChangeEvent<HTMLSelectElement>) => {
        onFieldChange('status', event.target.value as AnnouncementStatus);
    }, [ onFieldChange ]);

    const onCtaTargetTypeChange = useCallback((event: React.ChangeEvent<HTMLSelectElement>) => {
        onFieldChange('ctaTargetType', event.target.value as CtaTargetType);
    }, [ onFieldChange ]);

    const onEnabledChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        onFieldChange('enabled', event.target.checked);
    }, [ onFieldChange ]);

    const onMediaImageFileSelected = useCallback(async (event: React.ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        if (!file) {
            return;
        }

        if (!file.type.startsWith('image/')) {
            setPageError(true);
            setPageMessage('Please select a valid image file.');
            return;
        }

        try {
            const dataUrl = await readFileAsDataUrl(file);
            onFieldChange('mediaImageSource', dataUrl);
            setPageError(false);
            setPageMessage('Preview image uploaded. Save to publish the change.');
        } catch (error) {
            setPageError(true);
            setPageMessage(await parseErrorMessage(error));
        } finally {
            event.target.value = '';
        }
    }, [ onFieldChange ]);

    const onHeroGifFileSelected = useCallback(async (event: React.ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        if (!file) {
            return;
        }

        if (!file.type.startsWith('image/')) {
            setPageError(true);
            setPageMessage('Please select a valid image or gif file.');
            return;
        }

        try {
            const dataUrl = await readFileAsDataUrl(file);
            onFieldChange('heroGifSource', dataUrl);
            setPageError(false);
            setPageMessage('Hero media uploaded. Save to publish the change.');
        } catch (error) {
            setPageError(true);
            setPageMessage(await parseErrorMessage(error));
        } finally {
            event.target.value = '';
        }
    }, [ onFieldChange ]);

    const onSave = useCallback(async () => {
        const validationErrors = buildValidationErrors(formState);
        if (validationErrors.length > 0) {
            setPageError(true);
            setPageMessage(validationErrors[0]);
            return;
        }

        setIsSaving(true);
        setPageError(false);
        setPageMessage('');

        try {
            const saved = await upsertAdminFeatureAnnouncement({
                id: formState.id || undefined,
                campaignId: formState.campaignId,
                enabled: formState.enabled,
                status: formState.status,
                heading: formState.heading,
                title: formState.title,
                subtitle: formState.subtitle,
                description: formState.description,
                highlights: formState.highlightsText
                    .split('\n')
                    .map(line => line.trim())
                    .filter(Boolean),
                helpText: formState.helpText,
                heroGifSource: formState.heroGifSource,
                mediaImageSource: formState.mediaImageSource,
                mediaImageAlt: formState.mediaImageAlt,
                mediaImageCaption: formState.mediaImageCaption,
                ctaLabel: formState.ctaLabel,
                ctaTargetType: formState.ctaTargetType,
                ctaTarget: formState.ctaTarget,
                closeLabel: formState.closeLabel,
                startsAtUtc: formState.startsAtUtc,
                endsAtUtc: formState.endsAtUtc,
                maxImpressionsPerDay: formState.maxImpressionsPerDay,
                maxImpressionsTotal: formState.maxImpressionsTotal,
                priority: formState.priority,
                sortOrder: formState.sortOrder
            }, apiClient || undefined);

            setSelectedAnnouncementId(saved.id);
            setPageError(false);
            setPageMessage('Announcement saved successfully.');
            await refreshAnnouncements();
        } catch (error) {
            setPageError(true);
            setPageMessage(await parseErrorMessage(error));
        } finally {
            setIsSaving(false);
        }
    }, [ apiClient, formState, refreshAnnouncements ]);

    const onSaveClick = useCallback(() => {
        onSave().then(() => undefined, () => undefined);
    }, [ onSave ]);

    const onOpenPreview = useCallback(() => {
        setIsPreviewOpen(true);
    }, []);

    const onClosePreview = useCallback(() => {
        setIsPreviewOpen(false);
    }, []);

    const onAnnouncementListItemClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
        const announcementId = event.currentTarget.dataset.announcementId || '';
        if (!announcementId) {
            return;
        }

        const selectedAnnouncement = announcementsById.get(announcementId);
        if (!selectedAnnouncement) {
            return;
        }

        onSelectAnnouncement(selectedAnnouncement);
    }, [ announcementsById, onSelectAnnouncement ]);

    const previewCampaign = useMemo<FeatureAnnouncementCampaign>(() => {
        const previewAnnouncement: AdminFeatureAnnouncement = {
            id: formState.id,
            campaignId: formState.campaignId || 'announcement-preview',
            enabled: formState.enabled,
            status: formState.status,
            heading: formState.heading,
            title: formState.title || 'Announcement Preview',
            subtitle: formState.subtitle,
            description: formState.description || 'Preview content',
            highlights: formState.highlightsText
                .split('\n')
                .map(line => line.trim())
                .filter(Boolean),
            helpText: formState.helpText,
            heroGifSource: formState.heroGifSource,
            mediaImageSource: formState.mediaImageSource,
            mediaImageAlt: formState.mediaImageAlt,
            mediaImageCaption: formState.mediaImageCaption,
            ctaLabel: formState.ctaLabel,
            ctaTargetType: formState.ctaTargetType,
            ctaTarget: formState.ctaTarget,
            closeLabel: formState.closeLabel,
            startsAtUtc: formState.startsAtUtc,
            endsAtUtc: formState.endsAtUtc,
            maxImpressionsPerDay: formState.maxImpressionsPerDay,
            maxImpressionsTotal: formState.maxImpressionsTotal,
            priority: formState.priority,
            sortOrder: formState.sortOrder,
            createdAtUtc: formState.createdAtUtc,
            updatedAtUtc: formState.updatedAtUtc,
            createdByUserId: null,
            createdByUsername: formState.createdByUsername,
            updatedByUserId: null,
            updatedByUsername: formState.updatedByUsername
        };

        return toFeatureAnnouncementCampaignForPreview(previewAnnouncement);
    }, [ formState ]);

    return (
        <div className='announcementAdminRoot'>
            <header className='announcementAdminHeader'>
                <div>
                    <h1>Announcement</h1>
                    <p>Control popup content, media, scheduling, limits, and CTA behavior from one place.</p>
                </div>
                <div className='announcementAdminHeaderActions'>
                    <button type='button' onClick={onCreateAnnouncement}>
                        New Announcement
                    </button>
                    <button type='button' onClick={onOpenPreview}>
                        Preview
                    </button>
                    <button type='button' onClick={onSaveClick} disabled={isSaving}>
                        {isSaving ? 'Saving...' : 'Save'}
                    </button>
                </div>
            </header>

            {!!pageMessage && (
                <p className={`announcementAdminMessage${pageError ? ' error' : ''}`}>
                    {pageMessage}
                </p>
            )}

            <div className='announcementAdminLayout'>
                <aside className='announcementAdminListPanel'>
                    <h2>Announcements</h2>
                    {isLoading ? (
                        <p className='announcementAdminMuted'>Loading announcements...</p>
                    ) : (
                        <div className='announcementAdminList'>
                            {sortedAnnouncements.map(announcement => (
                                <button
                                    type='button'
                                    key={announcement.id}
                                    className={`announcementAdminListItem${announcement.id === selectedAnnouncementId ? ' selected' : ''}`}
                                    data-announcement-id={announcement.id}
                                    onClick={onAnnouncementListItemClick}
                                >
                                    <div className='announcementAdminListItemTop'>
                                        <strong>{announcement.title || announcement.campaignId}</strong>
                                        <span className={`announcementAdminStatusBadge ${announcement.status.toLowerCase()}`}>
                                            {announcement.status}
                                        </span>
                                    </div>
                                    <p>{announcement.campaignId}</p>
                                    <div className='announcementAdminListMeta'>
                                        <span>{announcement.enabled ? 'Enabled' : 'Disabled'}</span>
                                        <span>Priority {announcement.priority}</span>
                                    </div>
                                </button>
                            ))}
                        </div>
                    )}
                </aside>

                <section className='announcementAdminFormPanel'>
                    <div className='announcementAdminFormGrid'>
                        <label>
                            Campaign Id
                            <input
                                type='text'
                                name='campaignId'
                                value={formState.campaignId}
                                onChange={onTextInputChange}
                                placeholder='leaderboard-launch-2026'
                            />
                        </label>

                        <label>
                            Status
                            <select
                                value={formState.status}
                                onChange={onStatusChange}
                            >
                                <option value='Draft'>Draft</option>
                                <option value='Published'>Published</option>
                            </select>
                        </label>

                        <label className='announcementAdminToggleLabel'>
                            <input
                                type='checkbox'
                                checked={formState.enabled}
                                onChange={onEnabledChange}
                            />
                            Enabled
                        </label>

                        <label>
                            Heading
                            <input
                                type='text'
                                name='heading'
                                value={formState.heading}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label>
                            Title
                            <input
                                type='text'
                                name='title'
                                value={formState.title}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label>
                            Subtitle
                            <input
                                type='text'
                                name='subtitle'
                                value={formState.subtitle}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label className='span-two'>
                            Description
                            <textarea
                                name='description'
                                rows={4}
                                value={formState.description}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label className='span-two'>
                            Highlights (one per line)
                            <textarea
                                name='highlightsText'
                                rows={4}
                                value={formState.highlightsText}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label className='span-two'>
                            Help Text
                            <textarea
                                name='helpText'
                                rows={2}
                                value={formState.helpText}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label>
                            Hero GIF Source
                            <input
                                type='text'
                                name='heroGifSource'
                                value={formState.heroGifSource}
                                onChange={onTextInputChange}
                                placeholder='builtin:request-popup-accent or URL/data URL'
                            />
                        </label>

                        <label>
                            Upload Hero GIF/Image
                            <input
                                type='file'
                                accept='image/*,.gif'
                                onChange={onHeroGifFileSelected}
                            />
                        </label>

                        <label>
                            Media Image Source
                            <input
                                type='text'
                                name='mediaImageSource'
                                value={formState.mediaImageSource}
                                onChange={onTextInputChange}
                                placeholder='builtin:leaderboard-announcement-preview or URL/data URL'
                            />
                        </label>

                        <label>
                            Upload Media Image
                            <input
                                type='file'
                                accept='image/*'
                                onChange={onMediaImageFileSelected}
                            />
                        </label>

                        <label>
                            Media Alt Text
                            <input
                                type='text'
                                name='mediaImageAlt'
                                value={formState.mediaImageAlt}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label>
                            Media Caption
                            <input
                                type='text'
                                name='mediaImageCaption'
                                value={formState.mediaImageCaption}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label>
                            CTA Label
                            <input
                                type='text'
                                name='ctaLabel'
                                value={formState.ctaLabel}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label>
                            CTA Type
                            <select
                                value={formState.ctaTargetType}
                                onChange={onCtaTargetTypeChange}
                            >
                                <option value='InternalRoute'>Internal Route</option>
                                <option value='ExternalUrl'>External URL</option>
                            </select>
                        </label>

                        <label className='span-two'>
                            CTA Target
                            <input
                                type='text'
                                name='ctaTarget'
                                value={formState.ctaTarget}
                                onChange={onTextInputChange}
                                placeholder={formState.ctaTargetType === 'InternalRoute' ? '/achievements' : 'https://example.com'}
                            />
                        </label>

                        <label>
                            Close Button Label
                            <input
                                type='text'
                                name='closeLabel'
                                value={formState.closeLabel}
                                onChange={onTextInputChange}
                            />
                        </label>

                        <label>
                            Start Date (UTC)
                            <input
                                type='datetime-local'
                                name='startsAtUtc'
                                value={toDateTimeLocalValue(formState.startsAtUtc)}
                                onChange={onDateInputChange}
                            />
                        </label>

                        <label>
                            End Date (UTC)
                            <input
                                type='datetime-local'
                                name='endsAtUtc'
                                value={toDateTimeLocalValue(formState.endsAtUtc)}
                                onChange={onDateInputChange}
                            />
                        </label>

                        <label>
                            Max Impressions / Day
                            <input
                                type='number'
                                name='maxImpressionsPerDay'
                                min={1}
                                value={formState.maxImpressionsPerDay}
                                onChange={onNumberInputChange}
                            />
                        </label>

                        <label>
                            Max Impressions Total
                            <input
                                type='number'
                                name='maxImpressionsTotal'
                                min={1}
                                value={formState.maxImpressionsTotal}
                                onChange={onNumberInputChange}
                            />
                        </label>

                        <label>
                            Priority
                            <input
                                type='number'
                                name='priority'
                                value={formState.priority}
                                onChange={onNumberInputChange}
                            />
                        </label>

                        <label>
                            Sort Order
                            <input
                                type='number'
                                name='sortOrder'
                                value={formState.sortOrder}
                                onChange={onNumberInputChange}
                            />
                        </label>
                    </div>

                    <div className='announcementAdminAudit'>
                        <h3>Audit</h3>
                        <p>
                            Created by: <strong>{formState.createdByUsername || '—'}</strong>
                            {formState.createdAtUtc ? ` at ${new Date(formState.createdAtUtc).toLocaleString()}` : ''}
                        </p>
                        <p>
                            Updated by: <strong>{formState.updatedByUsername || '—'}</strong>
                            {formState.updatedAtUtc ? ` at ${new Date(formState.updatedAtUtc).toLocaleString()}` : ''}
                        </p>
                    </div>
                </section>
            </div>

            {isPreviewOpen && (
                <FeatureAnnouncementPopup
                    campaign={previewCampaign}
                    onCheckItOut={onClosePreview}
                    onClose={onClosePreview}
                />
            )}
        </div>
    );
};

export default AnnouncementAdmin;
