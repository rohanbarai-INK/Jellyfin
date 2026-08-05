import React, { useCallback, useEffect, useState } from 'react';

import { useApi } from 'hooks/useApi';
import type { AppDownloadConfig, SaveAppDownloadConfigPayload } from 'utils/appDownloadApi';
import { getAppDownloadConfig, saveAppDownloadConfig } from 'utils/appDownloadApi';
import {
    APK_DOWNLOAD_URL,
    APK_FILE_NAME,
    TV_APK_DOWNLOAD_URL,
    TV_APK_FILE_NAME
} from 'components/toolbar/DownloadAppTooltip';

import './appDownload.scss';

interface FormState {
    mobileApkUrl: string
    mobileApkFileName: string
    mobileIsNew: boolean
    tvApkUrl: string
    tvApkFileName: string
    tvIsNew: boolean
    maxNewInteractions: number
}

const DEFAULT_FORM: FormState = {
    mobileApkUrl: APK_DOWNLOAD_URL,
    mobileApkFileName: APK_FILE_NAME,
    mobileIsNew: false,
    tvApkUrl: TV_APK_DOWNLOAD_URL,
    tvApkFileName: TV_APK_FILE_NAME,
    tvIsNew: false,
    maxNewInteractions: 3
};

const AppDownloadAdmin = () => {
    const { __legacyApiClient__: apiClient } = useApi();

    const [ form, setForm ] = useState<FormState>(DEFAULT_FORM);
    const [ isLoading, setIsLoading ] = useState(true);
    const [ isSaving, setIsSaving ] = useState(false);
    const [ message, setMessage ] = useState('');
    const [ isError, setIsError ] = useState(false);
    const [ lastSaved, setLastSaved ] = useState<AppDownloadConfig | null>(null);

    const loadConfig = useCallback(async () => {
        setIsLoading(true);
        try {
            const config = await getAppDownloadConfig(apiClient || undefined);
            setLastSaved(config);
            setForm({
                mobileApkUrl: config.mobileApkUrl || APK_DOWNLOAD_URL,
                mobileApkFileName: config.mobileApkFileName || APK_FILE_NAME,
                mobileIsNew: config.mobileIsNew,
                tvApkUrl: config.tvApkUrl || TV_APK_DOWNLOAD_URL,
                tvApkFileName: config.tvApkFileName || TV_APK_FILE_NAME,
                tvIsNew: config.tvIsNew,
                maxNewInteractions: config.maxNewInteractions ?? 3
            });
        } catch {
            setIsError(true);
            setMessage('Failed to load current config. Showing defaults.');
        } finally {
            setIsLoading(false);
        }
    }, [ apiClient ]);

    useEffect(() => {
        void loadConfig();
    }, [ loadConfig ]);

    const onTextField = useCallback((field: keyof FormState) => (e: React.ChangeEvent<HTMLInputElement>) => {
        setForm(prev => ({ ...prev, [field]: e.target.value }));
    }, []);

    const onToggle = useCallback((field: 'mobileIsNew' | 'tvIsNew') => (e: React.ChangeEvent<HTMLInputElement>) => {
        setForm(prev => ({ ...prev, [field]: e.target.checked }));
    }, []);

    const onMaxInteractionsChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        const val = Math.max(1, Math.min(20, parseInt(e.target.value, 10) || 1));
        setForm(prev => ({ ...prev, maxNewInteractions: val }));
    }, []);

    const onSave = useCallback(async () => {
        setIsSaving(true);
        setMessage('');
        setIsError(false);
        try {
            const payload: SaveAppDownloadConfigPayload = {
                mobileApkUrl: form.mobileApkUrl.trim(),
                mobileApkFileName: form.mobileApkFileName.trim() || APK_FILE_NAME,
                mobileIsNew: form.mobileIsNew,
                tvApkUrl: form.tvApkUrl.trim(),
                tvApkFileName: form.tvApkFileName.trim() || TV_APK_FILE_NAME,
                tvIsNew: form.tvIsNew,
                maxNewInteractions: Math.max(1, form.maxNewInteractions)
            };
            const saved = await saveAppDownloadConfig(payload, apiClient || undefined);
            setLastSaved(saved);
            setMessage('Configuration saved successfully.');
        } catch {
            setIsError(true);
            setMessage('Failed to save configuration.');
        } finally {
            setIsSaving(false);
        }
    }, [ apiClient, form ]);

    const onSaveClick = useCallback(() => { onSave().then(() => undefined, () => undefined); }, [ onSave ]);

    return (
        <div className='appDownloadAdminRoot'>
            <header className='appDownloadAdminHeader'>
                <div>
                    <h1>App Downloads</h1>
                    <p>Manage APK download links and NEW badge visibility without redeploying.</p>
                </div>
                <button type='button' onClick={onSaveClick} disabled={isSaving || isLoading}>
                    {isSaving ? 'Saving…' : 'Save'}
                </button>
            </header>

            {!!message && (
                <p className={`appDownloadAdminMessage${isError ? ' error' : ''}`}>{message}</p>
            )}

            {isLoading ? (
                <p className='appDownloadAdminMuted'>Loading…</p>
            ) : (
                <div className='appDownloadAdminGrid'>

                    {/* ── Mobile App ── */}
                    <section className='appDownloadAdminCard'>
                        <div className='appDownloadAdminCardHeader'>
                            <span className='appDownloadAdminCardIcon mobile'>📱</span>
                            <h2>Mobile App</h2>
                            {form.mobileIsNew && <span className='appDownloadAdminNewPill'>NEW</span>}
                        </div>

                        <label>
                            Download URL
                            <input
                                type='url'
                                value={form.mobileApkUrl}
                                onChange={onTextField('mobileApkUrl')}
                                placeholder='https://www.dropbox.com/…?dl=1'
                            />
                        </label>

                        <label>
                            APK Filename (for download prompt)
                            <input
                                type='text'
                                value={form.mobileApkFileName}
                                onChange={onTextField('mobileApkFileName')}
                                placeholder='KnightFlix-v0.0.1.apk'
                            />
                        </label>

                        <label className='appDownloadAdminToggleLabel'>
                            <input
                                type='checkbox'
                                checked={form.mobileIsNew}
                                onChange={onToggle('mobileIsNew')}
                            />
                            Show <strong>NEW</strong> badge on Mobile App button
                        </label>
                    </section>

                    {/* ── Android TV App ── */}
                    <section className='appDownloadAdminCard'>
                        <div className='appDownloadAdminCardHeader'>
                            <span className='appDownloadAdminCardIcon tv'>📺</span>
                            <h2>Android TV App</h2>
                            {form.tvIsNew && <span className='appDownloadAdminNewPill'>NEW</span>}
                        </div>

                        <label>
                            Download URL
                            <input
                                type='url'
                                value={form.tvApkUrl}
                                onChange={onTextField('tvApkUrl')}
                                placeholder='https://www.dropbox.com/…?dl=1'
                            />
                        </label>

                        <label>
                            APK Filename (for download prompt)
                            <input
                                type='text'
                                value={form.tvApkFileName}
                                onChange={onTextField('tvApkFileName')}
                                placeholder='KnightFlixTV-v0.0.1.apk'
                            />
                        </label>

                        <label className='appDownloadAdminToggleLabel'>
                            <input
                                type='checkbox'
                                checked={form.tvIsNew}
                                onChange={onToggle('tvIsNew')}
                            />
                            Show <strong>NEW</strong> badge on Android TV App button
                        </label>
                    </section>

                    {/* ── NEW Badge Behaviour ── */}
                    <section className='appDownloadAdminCard appDownloadAdminPreviewCard'>
                        <div className='appDownloadAdminCardHeader'>
                            <span className='appDownloadAdminCardIcon mobile'>🔔</span>
                            <h2>NEW Badge Behaviour</h2>
                        </div>

                        <p className='appDownloadAdminMuted'>
                            After a user clicks a NEW-badged download button this many times, the badge disappears
                            for them on that device. The counter resets automatically when you update the APK URL.
                        </p>

                        <label>
                            Max interactions before badge is hidden
                            <div className='appDownloadAdminInteractionsRow'>
                                <input
                                    type='number'
                                    min={1}
                                    max={20}
                                    value={form.maxNewInteractions}
                                    onChange={onMaxInteractionsChange}
                                />
                                <span className='appDownloadAdminInteractionsHint'>
                                    {form.maxNewInteractions === 1
                                        ? 'Hidden after 1st click'
                                        : `Hidden after ${form.maxNewInteractions} clicks`}
                                </span>
                            </div>
                        </label>

                        <ul className='appDownloadAdminBehaviourList'>
                            <li>Set to <strong>1</strong> — user sees NEW only once per device</li>
                            <li>Set to <strong>3</strong> — user sees NEW on their first 3 visits</li>
                            <li>NEW resets automatically when you change the Download URL</li>
                        </ul>
                    </section>

                    {/* ── Preview & Audit ── */}
                    <section className='appDownloadAdminCard appDownloadAdminPreviewCard'>
                        <h2>Live Preview</h2>
                        <p className='appDownloadAdminMuted'>This is how the download buttons will appear to users.</p>

                        <div className='appDownloadAdminPreviewButtons'>
                            <div style={{ position: 'relative', display: 'inline-block' }}>
                                {form.mobileIsNew && (
                                    <span className='appDownloadAdminPreviewBadge'>NEW</span>
                                )}
                                <button type='button' className='appDownloadAdminPreviewBtn mobile'>
                                    📱 Mobile App
                                </button>
                            </div>
                            <div style={{ position: 'relative', display: 'inline-block' }}>
                                {form.tvIsNew && (
                                    <span className='appDownloadAdminPreviewBadge'>NEW</span>
                                )}
                                <button type='button' className='appDownloadAdminPreviewBtn tv'>
                                    📺 Android TV App
                                </button>
                            </div>
                        </div>

                        {lastSaved && lastSaved.updatedByUsername && (
                            <p className='appDownloadAdminMuted' style={{ marginTop: '16px', fontSize: '11px' }}>
                                Last saved by <strong>{lastSaved.updatedByUsername}</strong>
                                {lastSaved.updatedAtUtc
                                    ? ` on ${new Date(lastSaved.updatedAtUtc).toLocaleString()}`
                                    : ''}
                            </p>
                        )}
                    </section>
                </div>
            )}
        </div>
    );
};

export default AppDownloadAdmin;
