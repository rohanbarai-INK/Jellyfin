import React, { FC, useEffect, useRef, useState } from 'react';

import layoutManager from 'components/layoutManager';
import browser from 'scripts/browser';
import type { AppDownloadConfig } from 'utils/appDownloadApi';
import { getAppDownloadConfig } from 'utils/appDownloadApi';

import DownloadAppPopup from './DownloadAppPopup';
import DownloadAppTooltip, {
    APK_DOWNLOAD_URL,
    APK_FILE_NAME,
    TV_APK_DOWNLOAD_URL,
    TV_APK_FILE_NAME
} from './DownloadAppTooltip';

/**
 * Detects if the app is running inside the Jellyfin Android native WebView shell.
 * The native Android app injects window.NativeShell.
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

/**
 * Login-page download control.
 * Styled like the surrounding raised login actions, while reusing the shared
 * download tooltip + install-guide popup so APK links stay in sync with admin config.
 */
const DownloadAppLoginButton: FC = () => {
    const [showTooltip, setShowTooltip] = useState(false);
    const [showPopup, setShowPopup] = useState(false);
    const [config, setConfig] = useState<AppDownloadConfig>(DEFAULT_CONFIG);
    const [mobileCount, setMobileCount] = useState(0);
    const [tvCount, setTvCount] = useState(0);

    const btnRef = useRef<HTMLButtonElement>(null);
    const tooltipRef = useRef<HTMLDivElement>(null);
    const isTvMode = isAndroidTvClient();

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
                setMobileCount(getInteractionCount('mobile', resolved.mobileApkUrl));
                setTvCount(getInteractionCount('tv', resolved.tvApkUrl));
            })
            .catch(() => {
                // Fall back to hardcoded defaults when config is unavailable
            });
    }, []);

    const showMobileNew = config.mobileIsNew && mobileCount < config.maxNewInteractions;
    const showTvNew = config.tvIsNew && tvCount < config.maxNewInteractions;

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

    useEffect(() => {
        if (!showTooltip) {
            return;
        }

        const onKeyDown = (event: KeyboardEvent) => {
            if (event.key === 'Escape' || event.key === 'Backspace') {
                event.preventDefault();
                setShowTooltip(false);
                btnRef.current?.focus();
            }
        };

        document.addEventListener('keydown', onKeyDown);
        return () => document.removeEventListener('keydown', onKeyDown);
    }, [showTooltip]);

    if (isAndroidNativeApp() || isUnsupportedTvBrowser()) {
        return null;
    }

    return (
        <>
            <div className='downloadAppLoginAnchor'>
                <button
                    ref={btnRef}
                    type='button'
                    className='downloadAppLoginButton'
                    aria-label='Download KnightFlix App'
                    aria-expanded={showTooltip}
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
                >
                    <span className='downloadAppLoginButtonPulse' aria-hidden='true' />
                    <span className='downloadAppLoginButtonInner'>
                        <svg
                            className='downloadAppLoginButtonIcon'
                            fill='currentColor'
                            viewBox='0 0 24 24'
                            aria-hidden='true'
                        >
                            <path d='M12 2a1 1 0 0 1 1 1v10.586l2.293-2.293a1 1 0 1 1 1.414 1.414l-4 4a1 1 0 0 1-1.414 0l-4-4a1 1 0 1 1 1.414-1.414L11 13.586V3a1 1 0 0 1 1-1zM4 17a1 1 0 0 1 1 1v1h14v-1a1 1 0 1 1 2 0v1a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-1a1 1 0 0 1 1-1z' />
                        </svg>
                        <span className='downloadAppLoginButtonLabel'>Download App</span>
                        {(showMobileNew || showTvNew) && (
                            <span className='downloadAppLoginNewBadge'>NEW</span>
                        )}
                    </span>
                </button>

                {showTooltip && (
                    <>
                        <button
                            type='button'
                            className='downloadAppLoginBackdrop'
                            aria-label='Close download panel'
                            onClick={() => setShowTooltip(false)}
                        />
                        <div className={`downloadAppLoginTooltipHost${isTvMode ? ' downloadAppLoginTooltipHost-tv' : ''}`}>
                            <DownloadAppTooltip
                                ref={tooltipRef}
                                config={config}
                                isTvMode={isTvMode}
                                placement='login'
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
                        </div>
                    </>
                )}
            </div>

            {showPopup && (
                <DownloadAppPopup onClose={() => setShowPopup(false)} />
            )}
        </>
    );
};

export default DownloadAppLoginButton;
