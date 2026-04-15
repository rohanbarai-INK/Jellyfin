import React, { forwardRef } from 'react';

import appIconUrl from 'assets/branding/icon-transparent.png';

// ─── Config ──────────────────────────────────────────────────────────────────
// To change the APK download link, update the APK_DOWNLOAD_URL constant below.
export const APK_DOWNLOAD_URL = 'https://www.dropbox.com/scl/fi/lt80pztxgnfo28juvqfdy/KnightFlix-v0.0.1.apk?rlkey=9mizp5qaqrvr7a0tujx5wcm5i&st=6hyvbtrm&dl=1';
export const APK_FILE_NAME = 'KnightFlix-v0.0.1.apk';

interface DownloadAppTooltipProps {
    onDownloadClick: () => void;
}

const DownloadAppTooltip = forwardRef<HTMLDivElement, DownloadAppTooltipProps>(
    ({ onDownloadClick }, ref) => {
        const handleDownload = () => {
            const link = document.createElement('a');
            link.href = APK_DOWNLOAD_URL;
            link.download = APK_FILE_NAME;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            onDownloadClick();
        };

        return (
            <div
                ref={ref}
                style={{
                    position: 'absolute',
                    right: 0,
                    top: '48px',
                    zIndex: 1400,
                    width: '288px',
                    animation: 'kfTooltipSlideIn 0.22s cubic-bezier(.21,1.02,.73,1) both'
                }}
            >
                {/* Arrow pointing up */}
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

                {/* Card */}
                <div style={{
                    background: '#1c1c28',
                    border: '1px solid rgba(255,255,255,0.1)',
                    borderRadius: '16px',
                    boxShadow: '0 20px 60px rgba(0,0,0,0.6)',
                    overflow: 'hidden'
                }}>
                    {/* Top gradient strip */}
                    <div style={{
                        height: '4px',
                        background: 'linear-gradient(to right, #facc15, #fb923c, #fde047)',
                        width: '100%'
                    }} />

                    <div style={{ padding: '16px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
                        {/* Header: icon + app name */}
                        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                            <div style={{
                                width: '40px',
                                height: '40px',
                                borderRadius: '12px',
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
                                    style={{ width: '28px', height: '28px', objectFit: 'contain' }}
                                />
                            </div>
                            <div>
                                <p style={{
                                    margin: 0,
                                    color: '#fff',
                                    fontWeight: 600,
                                    fontSize: '14px',
                                    lineHeight: 1.3
                                }}>
                                    KnightFlix for Android
                                </p>
                                <p style={{
                                    margin: 0,
                                    color: 'rgba(255,255,255,0.4)',
                                    fontSize: '12px',
                                    marginTop: '2px'
                                }}>
                                    Free · Direct APK download
                                </p>
                            </div>
                        </div>

                        {/* Feature pills */}
                        <div style={{ display: 'flex', flexWrap: 'wrap' as const, gap: '6px' }}>
                            {['HD Streaming', 'Offline Mode', 'Multi-Screen'].map((f) => (
                                <span key={f} style={{
                                    fontSize: '10px',
                                    fontWeight: 500,
                                    padding: '2px 8px',
                                    borderRadius: '999px',
                                    background: 'rgba(255,255,255,0.05)',
                                    border: '1px solid rgba(255,255,255,0.1)',
                                    color: 'rgba(255,255,255,0.6)'
                                }}>
                                    {f}
                                </span>
                            ))}
                        </div>

                        {/* Divider */}
                        <div style={{ borderTop: '1px solid rgba(255,255,255,0.06)' }} />

                        {/* CTA Button */}
                        <button
                            onClick={handleDownload}
                            style={{
                                width: '100%',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                gap: '10px',
                                padding: '10px 0',
                                borderRadius: '12px',
                                background: 'linear-gradient(to right, #facc15, #fb923c)',
                                border: 'none',
                                cursor: 'pointer',
                                color: '#000',
                                fontWeight: 700,
                                fontSize: '14px',
                                boxShadow: '0 4px 20px rgba(250,204,21,0.25)',
                                transition: 'transform 0.15s, box-shadow 0.15s'
                            }}
                            onMouseEnter={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.02)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 24px rgba(250,204,21,0.4)';
                            }}
                            onMouseLeave={e => {
                                (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)';
                                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 20px rgba(250,204,21,0.25)';
                            }}
                        >
                            <svg style={{ width: '16px', height: '16px' }} fill='currentColor' viewBox='0 0 24 24'>
                                <path d='M12 2a1 1 0 0 1 1 1v10.586l2.293-2.293a1 1 0 1 1 1.414 1.414l-4 4a1 1 0 0 1-1.414 0l-4-4a1 1 0 1 1 1.414-1.414L11 13.586V3a1 1 0 0 1 1-1zM4 17a1 1 0 0 1 1 1v1h14v-1a1 1 0 1 1 2 0v1a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-1a1 1 0 0 1 1-1z' />
                            </svg>
                            Download APK
                        </button>

                        <p style={{
                            margin: 0,
                            textAlign: 'center',
                            fontSize: '10px',
                            color: 'rgba(255,255,255,0.3)',
                            lineHeight: 1.4
                        }}>
                            Not on Play Store · Install guide opens after download
                        </p>
                    </div>
                </div>

                <style>{`
                    @keyframes kfTooltipSlideIn {
                        from { opacity: 0; transform: translateY(-8px) scale(0.95); }
                        to   { opacity: 1; transform: translateY(0) scale(1); }
                    }
                `}</style>
            </div>
        );
    }
);

DownloadAppTooltip.displayName = 'DownloadAppTooltip';
export default DownloadAppTooltip;
