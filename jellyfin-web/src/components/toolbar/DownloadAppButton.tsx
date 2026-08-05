import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import React, { FC, useState, useRef, useEffect } from 'react';

import layoutManager from 'components/layoutManager';
import browser from 'scripts/browser';
import type { AppDownloadConfig } from 'utils/appDownloadApi';
import { getAppDownloadConfig } from 'utils/appDownloadApi';
import { APK_DOWNLOAD_URL, APK_FILE_NAME, TV_APK_DOWNLOAD_URL, TV_APK_FILE_NAME } from './DownloadAppTooltip';
import DownloadAppPopup from './DownloadAppPopup';
import DownloadAppTooltip from './DownloadAppTooltip';

/**
 * Detects if the app is running inside the Jellyfin Android native WebView shell.
 * The native Android app injects window.NativeShell.
 * We do NOT show the download button in this environment.
 */
function isAndroidNativeApp(): boolean {
    return Boolean((window as Window & { NativeShell?: unknown }).NativeShell);
}

function isAndroidTvClient(): boolean {
    return Boolean(browser.android && (browser.tv || layoutManager.tv));
}

function isUnsupportedTvBrowser(): boolean {
    return Boolean(browser.tv && !isAndroidTvClient());
}

// ─── localStorage interaction tracking ──────────────────────────────────────
// Key is derived from the APK URL (not timestamp) so the count automatically
// resets when the admin pushes a new APK URL, without resetting on metadata-only saves.
function interactionKey(type: 'mobile' | 'tv', apkUrl: string): string {
    const sig = apkUrl.replace(/[^a-zA-Z0-9]/g, '').slice(0, 36);
    return `kf_apk_new_${type}_${sig}`;
}

function getInteractionCount(type: 'mobile' | 'tv', apkUrl: string): number {
    try {
        return parseInt(localStorage.getItem(interactionKey(type, apkUrl)) ?? '0', 10) || 0;
    } catch {
        return 0;
    }
}

function incrementInteractionCount(type: 'mobile' | 'tv', apkUrl: string): number {
    const key = interactionKey(type, apkUrl);
    const next = getInteractionCount(type, apkUrl) + 1;
    try { localStorage.setItem(key, String(next)); } catch { /* ignore */ }
    return next;
}
// ─────────────────────────────────────────────────────────────────────────────

const DEFAULT_CONFIG: AppDownloadConfig = {
    mobileApkUrl: APK_DOWNLOAD_URL,
    mobileApkFileName: APK_FILE_NAME,
    mobileIsNew: false,
    tvApkUrl: TV_APK_DOWNLOAD_URL,
    tvApkFileName: TV_APK_FILE_NAME,
    tvIsNew: false,
    maxNewInteractions: 3,
    updatedAtUtc: '',
    updatedByUsername: ''
};

const DownloadAppButton: FC = () => {
    const [showTooltip, setShowTooltip] = useState(false);
    const [showPopup, setShowPopup] = useState(false);
    const [config, setConfig] = useState<AppDownloadConfig>(DEFAULT_CONFIG);
    // Track interaction counts as state so re-render fires when incremented
    const [mobileCount, setMobileCount] = useState(0);
    const [tvCount, setTvCount] = useState(0);

    const btnRef = useRef<HTMLButtonElement>(null);
    const tooltipRef = useRef<HTMLDivElement>(null);
    const isTvMode = isAndroidTvClient();

    // Fetch admin-configured download URLs on mount
    useEffect(() => {
        getAppDownloadConfig()
            .then(fetched => {
                const resolved: AppDownloadConfig = {
                    mobileApkUrl: fetched.mobileApkUrl || APK_DOWNLOAD_URL,
                    mobileApkFileName: fetched.mobileApkFileName || APK_FILE_NAME,
                    mobileIsNew: fetched.mobileIsNew,
                    tvApkUrl: fetched.tvApkUrl || TV_APK_DOWNLOAD_URL,
                    tvApkFileName: fetched.tvApkFileName || TV_APK_FILE_NAME,
                    tvIsNew: fetched.tvIsNew,
                    maxNewInteractions: fetched.maxNewInteractions,
                    updatedAtUtc: fetched.updatedAtUtc,
                    updatedByUsername: fetched.updatedByUsername
                };
                setConfig(resolved);
                // Initialise counts from localStorage on config load
                setMobileCount(getInteractionCount('mobile', resolved.mobileApkUrl));
                setTvCount(getInteractionCount('tv', resolved.tvApkUrl));
            })
            .catch(() => {
                // Silently fall back to hardcoded defaults
            });
    }, []);

    // ── Derived visibility: admin enabled NEW AND user hasn't exhausted their quota
    const showMobileNew = config.mobileIsNew && mobileCount < config.maxNewInteractions;
    const showTvNew = config.tvIsNew && tvCount < config.maxNewInteractions;

    // Close tooltip on outside click
    useEffect(() => {
        const handler = (e: MouseEvent) => {
            if (
                tooltipRef.current && !tooltipRef.current.contains(e.target as Node) &&
                btnRef.current && !btnRef.current.contains(e.target as Node)
            ) {
                setShowTooltip(false);
            }
        };
        document.addEventListener('mousedown', handler);
        return () => document.removeEventListener('mousedown', handler);
    }, []);

    if (isAndroidNativeApp() || isUnsupportedTvBrowser()) {
        return null;
    }

    return (
        <>
            {/* Wrapper keeps position:relative so the tooltip can anchor below */}
            <div style={{ position: 'relative', display: 'inline-flex' }}>
                <Tooltip title='Download KnightFlix App'>
                    <IconButton
                        ref={btnRef}
                        size='large'
                        aria-label='Download KnightFlix App'
                        style={{ marginRight: isTvMode ? '14px' : '10px' }}
                        onClick={() => setShowTooltip(v => !v)}
                        onKeyDown={(event) => {
                            if (!isTvMode) {
                                return;
                            }

                            if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
                                event.preventDefault();
                                setShowTooltip(true);
                            }

                            if (event.key === 'Escape' || event.key === 'Backspace') {
                                event.preventDefault();
                                setShowTooltip(false);
                            }
                        }}
                        sx={{
                            color: '#facc15',
                            position: 'relative',
                            overflow: 'visible',
                            width: isTvMode ? 56 : undefined,
                            height: isTvMode ? 56 : undefined,
                            '&::before': {
                                content: '""',
                                position: 'absolute',
                                inset: '6px',
                                borderRadius: '50%',
                                border: '1px solid rgba(250,204,21,0.35)',
                                transition: 'border-color 0.2s'
                            },
                            '&:hover': {
                                color: '#fde047',
                                backgroundColor: 'rgba(250,204,21,0.08)',
                                '&::before': {
                                    borderColor: 'rgba(250,204,21,0.65)'
                                }
                            },
                            '&.Mui-focusVisible': {
                                color: '#fde047',
                                backgroundColor: 'rgba(250,204,21,0.12)',
                                boxShadow: isTvMode ? '0 0 0 3px rgba(250,204,21,0.45)' : 'none',
                                '&::before': {
                                    borderColor: 'rgba(250,204,21,0.8)'
                                }
                            }
                        }}
                    >
                        {/* Animated pulse ring */}
                        <span style={{
                            position: 'absolute',
                            inset: '6px',
                            borderRadius: '50%',
                            background: 'rgba(250,204,21,0.18)',
                            animation: 'kfBtnPing 2.5s cubic-bezier(0,0,0.2,1) infinite',
                            pointerEvents: 'none'
                        }} />

                        {/* Download arrow — same 24px viewport as MUI SvgIcon */}
                        <svg
                            style={{ width: '24px', height: '24px', position: 'relative', zIndex: 1 }}
                            fill='currentColor'
                            viewBox='0 0 24 24'
                        >
                            <path d='M12 2a1 1 0 0 1 1 1v10.586l2.293-2.293a1 1 0 1 1 1.414 1.414l-4 4a1 1 0 0 1-1.414 0l-4-4a1 1 0 1 1 1.414-1.414L11 13.586V3a1 1 0 0 1 1-1zM4 17a1 1 0 0 1 1 1v1h14v-1a1 1 0 1 1 2 0v1a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-1a1 1 0 0 1 1-1z' />
                        </svg>
                    </IconButton>
                </Tooltip>

                {/* NEW badge dot on the nav icon while at least one NEW badge is still visible */}
                {(showMobileNew || showTvNew) && (
                    <span style={{
                        position: 'absolute',
                        top: '6px',
                        right: '6px',
                        width: '8px',
                        height: '8px',
                        borderRadius: '50%',
                        background: '#22c55e',
                        border: '2px solid #121212',
                        zIndex: 2,
                        animation: 'kfNewDotPing 2s ease infinite'
                    }} />
                )}

                {/* Floating tooltip card anchored below the button */}
                {showTooltip && (
                    <DownloadAppTooltip
                        ref={tooltipRef}
                        config={config}
                        isTvMode={isTvMode}
                        showMobileNew={showMobileNew}
                        showTvNew={showTvNew}
                        onRequestClose={() => {
                            setShowTooltip(false);
                            btnRef.current?.focus();
                        }}
                        onMobileDownload={() => {
                            const next = incrementInteractionCount('mobile', config.mobileApkUrl);
                            setMobileCount(next);
                            setShowTooltip(false);
                            setShowPopup(true);
                        }}
                        onTvDownload={() => {
                            const next = incrementInteractionCount('tv', config.tvApkUrl);
                            setTvCount(next);
                            setShowTooltip(false);
                            setShowPopup(true);
                        }}
                    />
                )}
            </div>

            {/* Install guide modal */}
            {showPopup && (
                <DownloadAppPopup onClose={() => setShowPopup(false)} />
            )}

            <style>{`
                @keyframes kfBtnPing {
                    0%   { transform: scale(1);   opacity: 0.5; }
                    70%  { transform: scale(1.9); opacity: 0; }
                    100% { transform: scale(1.9); opacity: 0; }
                }
                @keyframes kfNewDotPing {
                    0%   { box-shadow: 0 0 0 0 rgba(34,197,94,0.7); }
                    70%  { box-shadow: 0 0 0 6px rgba(34,197,94,0); }
                    100% { box-shadow: 0 0 0 0 rgba(34,197,94,0); }
                }
            `}</style>
        </>
    );
};

export default DownloadAppButton;
