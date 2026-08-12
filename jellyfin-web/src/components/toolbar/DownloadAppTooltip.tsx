import React, { forwardRef, useEffect, useRef } from 'react';

import appIconUrl from 'assets/branding/icon-transparent.png';
import type { AppDownloadConfig } from 'utils/appDownloadApi';

// ─── Fallback Config ─────────────────────────────────────────────────────────
// These constants are used as fallbacks when the admin hasn't configured URLs yet.
export const APK_DOWNLOAD_URL = 'https://www.dropbox.com/scl/fi/lt80pztxgnfo28juvqfdy/KnightFlix-v0.0.1.apk?rlkey=9mizp5qaqrvr7a0tujx5wcm5i&st=6hyvbtrm&dl=1';
export const APK_FILE_NAME = 'KnightFlix-v0.0.1.apk';

export const TV_APK_DOWNLOAD_URL = 'https://www.dropbox.com/scl/fi/kshxwsf9vuy2r62bhhdmt/KnightFlixTV-v.0.0.1.apk?rlkey=x0eaigq4fxjzjdrw9otws3url&st=qehuzmlm&dl=1';
export const TV_APK_FILE_NAME = 'KnightFlixTV-v0.0.1.apk';

interface DownloadAppTooltipProps {
    config: AppDownloadConfig;
    isTvMode: boolean;
    showMobileNew: boolean;
    showTvNew: boolean;
    /** toolbar = navbar dropdown; login = centered under login-page action */
    placement?: 'toolbar' | 'login';
    onRequestClose: () => void;
    onMobileDownload: () => void;
    onTvDownload: () => void;
}

const DownloadAppTooltip = forwardRef<HTMLDivElement, DownloadAppTooltipProps>(
    ({ config, isTvMode, showMobileNew, showTvNew, placement = 'toolbar', onRequestClose, onMobileDownload, onTvDownload }, ref) => {
        const isLoginPlacement = placement === 'login' && !isTvMode;
        // Login uses fixed centering so it never clips under overflow parents on mobile.
        const isCenteredOverlay = isTvMode || isLoginPlacement;
        const centerTransform = isLoginPlacement
            ? 'translate(-50%, -50%)'
            : isTvMode
                ? 'translateX(-50%)'
                : undefined;
        const slideInAnimation = isLoginPlacement
            ? 'kfTooltipSlideInCentered 0.22s cubic-bezier(.21,1.02,.73,1) both'
            : isTvMode
                ? 'kfTooltipSlideInTv 0.22s cubic-bezier(.21,1.02,.73,1) both'
                : 'kfTooltipSlideIn 0.22s cubic-bezier(.21,1.02,.73,1) both';
        const mobileButtonRef = useRef<HTMLButtonElement>(null);
        const tvButtonRef = useRef<HTMLButtonElement>(null);

        const handleDownload = () => {
            const link = document.createElement('a');
            link.href = config.mobileApkUrl || APK_DOWNLOAD_URL;
            link.download = config.mobileApkFileName || APK_FILE_NAME;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            onMobileDownload();
        };

        const handleTvDownload = () => {
            const link = document.createElement('a');
            link.href = config.tvApkUrl || TV_APK_DOWNLOAD_URL;
            link.download = config.tvApkFileName || TV_APK_FILE_NAME;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            onTvDownload();
        };

        useEffect(() => {
            if (!isTvMode) {
                return;
            }

            tvButtonRef.current?.focus();
        }, [ isTvMode ]);

        const focusButton = (target: 'mobile' | 'tv') => {
            if (target === 'mobile') {
                mobileButtonRef.current?.focus();
                return;
            }

            tvButtonRef.current?.focus();
        };

        const handleKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
            if (!isTvMode) {
                return;
            }

            if (event.key === 'ArrowUp' || event.key === 'ArrowLeft') {
                event.preventDefault();
                focusButton('mobile');
                return;
            }

            if (event.key === 'ArrowDown' || event.key === 'ArrowRight') {
                event.preventDefault();
                focusButton('tv');
                return;
            }

            if (event.key === 'Escape' || event.key === 'Backspace' || event.key === 'GoBack') {
                event.preventDefault();
                onRequestClose();
            }
        };

        return (
            <div
                ref={ref}
                onKeyDown={handleKeyDown}
                style={{
                    position: isCenteredOverlay ? 'fixed' : 'absolute',
                    right: isCenteredOverlay ? 'auto' : 0,
                    left: isCenteredOverlay ? '50%' : 'auto',
                    top: isLoginPlacement ? '50%' : isTvMode ? '88px' : '48px',
                    zIndex: 1400,
                    width: isTvMode ? '440px' : isLoginPlacement ? 'min(320px, calc(100vw - 24px))' : '288px',
                    maxWidth: 'calc(100vw - 24px)',
                    maxHeight: isLoginPlacement ? 'min(520px, calc(100vh - 24px))' : undefined,
                    overflowY: isLoginPlacement ? 'auto' : undefined,
                    WebkitOverflowScrolling: isLoginPlacement ? 'touch' : undefined,
                    transform: centerTransform,
                    animation: slideInAnimation,
                    boxSizing: 'border-box'
                }}
            >
                {!isCenteredOverlay && (
                    <div style={{
                        position: 'absolute',
                        top: '-7px',
                        right: '12px',
                        width: '14px',
                        height: '14px',
                        background: '#1c1c28',
                        border: '1px solid rgba(255,255,255,0.1)',
                        borderRight: 'none',
                        borderBottom: 'none',
                        transform: 'rotate(45deg)',
                        borderRadius: '2px'
                    }} />
                )}

                <div style={{
                    background: '#1c1c28',
                    border: '1px solid rgba(255,255,255,0.1)',
                    borderRadius: isTvMode ? '22px' : '16px',
                    boxShadow: isTvMode ? '0 24px 80px rgba(0,0,0,0.7)' : '0 20px 60px rgba(0,0,0,0.6)',
                    overflow: 'hidden'
                }}>
                    <div style={{
                        height: isTvMode ? '6px' : '4px',
                        background: isTvMode
                            ? 'linear-gradient(to right, #7c3aed, #a855f7, #facc15)'
                            : 'linear-gradient(to right, #facc15, #fb923c, #fde047)',
                        width: '100%'
                    }} />

                    <div style={{ padding: isTvMode ? '24px' : '16px', display: 'flex', flexDirection: 'column', gap: isTvMode ? '16px' : '12px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: isTvMode ? '14px' : '10px' }}>
                            <div style={{
                                width: isTvMode ? '56px' : '40px',
                                height: isTvMode ? '56px' : '40px',
                                borderRadius: isTvMode ? '16px' : '12px',
                                background: 'rgba(250,204,21,0.1)',
                                border: '1px solid rgba(250,204,21,0.2)',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                flexShrink: 0,
                                overflow: 'hidden'
                            }}>
                                <img
                                    src={appIconUrl}
                                    alt='KnightFlix'
                                    style={{ width: isTvMode ? '38px' : '28px', height: isTvMode ? '38px' : '28px', objectFit: 'contain' }}
                                />
                            </div>
                            <div style={{ minWidth: 0, flex: 1 }}>
                                <p style={{
                                    margin: 0,
                                    color: '#fff',
                                    fontWeight: 600,
                                    fontSize: isTvMode ? '20px' : '14px',
                                    lineHeight: 1.3,
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap'
                                }}>
                                    {isTvMode ? 'KnightFlix Downloads for Android TV' : 'KnightFlix for Android'}
                                </p>
                                <p style={{
                                    margin: 0,
                                    color: 'rgba(255,255,255,0.4)',
                                    fontSize: isTvMode ? '14px' : '12px',
                                    marginTop: '2px',
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap'
                                }}>
                                    {isTvMode ? 'Remote-friendly APK download flow' : 'Free · Direct APK download'}
                                </p>
                            </div>
                        </div>

                        <div style={{ display: 'flex', flexWrap: 'wrap' as const, gap: '6px' }}>
                            {(isTvMode ? ['D-pad Ready', 'Direct APK', 'Android TV'] : ['HD Streaming', 'Offline Mode', 'Multi-Screen']).map((f) => (
                                <span key={f} style={{
                                    fontSize: isTvMode ? '12px' : '10px',
                                    fontWeight: 500,
                                    padding: isTvMode ? '4px 10px' : '2px 8px',
                                    borderRadius: '999px',
                                    background: 'rgba(255,255,255,0.05)',
                                    border: '1px solid rgba(255,255,255,0.1)',
                                    color: 'rgba(255,255,255,0.6)'
                                }}>
                                    {f}
                                </span>
                            ))}
                        </div>

                        <div style={{ borderTop: '1px solid rgba(255,255,255,0.06)' }} />

                        <button
                            ref={mobileButtonRef}
                            onClick={handleDownload}
                            style={{
                                position: 'relative',
                                width: '100%',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                gap: '10px',
                                padding: isTvMode ? '18px 16px' : '10px 0',
                                borderRadius: isTvMode ? '16px' : '12px',
                                background: 'linear-gradient(to right, #facc15, #fb923c)',
                                border: isTvMode ? '2px solid transparent' : 'none',
                                cursor: 'pointer',
                                color: '#000',
                                fontWeight: 700,
                                fontSize: isTvMode ? '18px' : '14px',
                                boxShadow: '0 4px 20px rgba(250,204,21,0.25)',
                                transition: 'transform 0.15s, box-shadow 0.15s, border-color 0.15s',
                                outline: 'none'
                            }}
                            onMouseEnter={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.02)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 24px rgba(250,204,21,0.4)';
                            }}
                            onMouseLeave={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 20px rgba(250,204,21,0.25)';
                                (e.currentTarget as HTMLButtonElement).style.borderColor = 'transparent';
                            }}
                            onFocus={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.02)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 0 0 4px rgba(250,204,21,0.3), 0 6px 26px rgba(250,204,21,0.45)';
                                (e.currentTarget as HTMLButtonElement).style.borderColor = 'rgba(255,255,255,0.7)';
                            }}
                            onBlur={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 20px rgba(250,204,21,0.25)';
                                (e.currentTarget as HTMLButtonElement).style.borderColor = 'transparent';
                            }}
                        >
                            {showMobileNew && (
                                <span style={{
                                    position: 'absolute',
                                    top: isTvMode ? '-10px' : '-8px',
                                    right: '10px',
                                    background: '#22c55e',
                                    color: '#fff',
                                    fontSize: isTvMode ? '11px' : '9px',
                                    fontWeight: 800,
                                    padding: isTvMode ? '4px 8px' : '2px 6px',
                                    borderRadius: '999px',
                                    letterSpacing: '0.05em',
                                    textTransform: 'uppercase',
                                    animation: 'kfNewBadgePop 0.4s cubic-bezier(.21,1.02,.73,1) both'
                                }}>NEW</span>
                            )}
                            <svg style={{ width: isTvMode ? '20px' : '16px', height: isTvMode ? '20px' : '16px' }} fill='currentColor' viewBox='0 0 24 24'>
                                <path d='M17.523 2.236l1.644 2.84a.5.5 0 0 1-.183.684.5.5 0 0 1-.684-.183L16.6 2.8a9.14 9.14 0 0 0-4.6-1.3 9.14 9.14 0 0 0-4.6 1.3L5.7 5.577a.5.5 0 0 1-.684.183.5.5 0 0 1-.183-.684l1.644-2.84C3.06 4.2 1 7.6 1 11.5h22c0-3.9-2.06-7.3-5.477-9.264zM7 9a1 1 0 1 1 0-2 1 1 0 0 1 0 2zm10 0a1 1 0 1 1 0-2 1 1 0 0 1 0 2zM1 12v7a2 2 0 0 0 2 2h18a2 2 0 0 0 2-2v-7H1z' />
                            </svg>
                            Mobile App
                        </button>

                        <button
                            ref={tvButtonRef}
                            onClick={handleTvDownload}
                            style={{
                                position: 'relative',
                                width: '100%',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                gap: '10px',
                                padding: isTvMode ? '18px 16px' : '10px 0',
                                borderRadius: isTvMode ? '16px' : '12px',
                                background: 'linear-gradient(to right, #7c3aed, #a855f7)',
                                border: isTvMode ? '2px solid transparent' : 'none',
                                cursor: 'pointer',
                                color: '#fff',
                                fontWeight: 700,
                                fontSize: isTvMode ? '18px' : '14px',
                                boxShadow: '0 4px 20px rgba(124,58,237,0.25)',
                                transition: 'transform 0.15s, box-shadow 0.15s, border-color 0.15s',
                                outline: 'none'
                            }}
                            onMouseEnter={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.02)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 24px rgba(124,58,237,0.4)';
                            }}
                            onMouseLeave={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 20px rgba(124,58,237,0.25)';
                                (e.currentTarget as HTMLButtonElement).style.borderColor = 'transparent';
                            }}
                            onFocus={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.02)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 0 0 4px rgba(168,85,247,0.32), 0 6px 26px rgba(124,58,237,0.45)';
                                (e.currentTarget as HTMLButtonElement).style.borderColor = 'rgba(255,255,255,0.7)';
                            }}
                            onBlur={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 20px rgba(124,58,237,0.25)';
                                (e.currentTarget as HTMLButtonElement).style.borderColor = 'transparent';
                            }}
                        >
                            {showTvNew && (
                                <span style={{
                                    position: 'absolute',
                                    top: isTvMode ? '-10px' : '-8px',
                                    right: '10px',
                                    background: '#22c55e',
                                    color: '#fff',
                                    fontSize: isTvMode ? '11px' : '9px',
                                    fontWeight: 800,
                                    padding: isTvMode ? '4px 8px' : '2px 6px',
                                    borderRadius: '999px',
                                    letterSpacing: '0.05em',
                                    textTransform: 'uppercase',
                                    animation: 'kfNewBadgePop 0.4s cubic-bezier(.21,1.02,.73,1) both'
                                }}>NEW</span>
                            )}
                            <svg style={{ width: isTvMode ? '20px' : '16px', height: isTvMode ? '20px' : '16px' }} fill='currentColor' viewBox='0 0 24 24'>
                                <path d='M21 3H3a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h7v2H8v2h8v-2h-2v-2h7a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2zm0 14H3V5h18v12z' />
                            </svg>
                            Android TV App
                        </button>

                        <p style={{
                            margin: 0,
                            textAlign: 'center',
                            fontSize: isTvMode ? '12px' : '10px',
                            color: 'rgba(255,255,255,0.3)',
                            lineHeight: 1.4
                        }}>
                            {isTvMode
                                ? 'Use D-pad and OK to choose a download · Back closes this panel'
                                : 'Not on Play Store · Install guide opens after download'}
                        </p>
                    </div>
                </div>

                <style>{`
                    @keyframes kfTooltipSlideIn {
                        from { opacity: 0; transform: translateY(-8px) scale(0.95); }
                        to   { opacity: 1; transform: translateY(0) scale(1); }
                    }
                    @keyframes kfTooltipSlideInTv {
                        from { opacity: 0; transform: translateX(-50%) translateY(-8px) scale(0.95); }
                        to   { opacity: 1; transform: translateX(-50%) translateY(0) scale(1); }
                    }
                    @keyframes kfTooltipSlideInCentered {
                        from { opacity: 0; transform: translate(-50%, -46%) scale(0.96); }
                        to   { opacity: 1; transform: translate(-50%, -50%) scale(1); }
                    }
                    @keyframes kfNewBadgePop {
                        from { opacity: 0; transform: scale(0.5); }
                        to   { opacity: 1; transform: scale(1); }
                    }
                `}</style>
            </div>
        );
    }
);

DownloadAppTooltip.displayName = 'DownloadAppTooltip';
export default DownloadAppTooltip;
