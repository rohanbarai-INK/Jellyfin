import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import React, { FC, useState, useRef, useEffect } from 'react';

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

/**
 * Detects TV browsers (SmartTV, etc.) where the download button is irrelevant.
 */
function isTvBrowser(): boolean {
    const ua = navigator.userAgent.toLowerCase();
    return ua.includes('tv') || ua.includes('samsungbrowser') || ua.includes('web0s') || ua.includes('netcast');
}

const DownloadAppButton: FC = () => {
    const [showTooltip, setShowTooltip] = useState(false);
    const [showPopup, setShowPopup] = useState(false);

    const btnRef = useRef<HTMLButtonElement>(null);
    const tooltipRef = useRef<HTMLDivElement>(null);

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

    // Do NOT render in Android native app or TV browsers
    if (isAndroidNativeApp() || isTvBrowser()) {
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
                        style={{ marginRight: '10px' }}
                        onClick={() => setShowTooltip(v => !v)}
                        sx={{
                            color: '#facc15',
                            position: 'relative',
                            overflow: 'visible',
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

                {/* Floating tooltip card anchored below the button */}
                {showTooltip && (
                    <DownloadAppTooltip
                        ref={tooltipRef}
                        onDownloadClick={() => {
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
            `}</style>
        </>
    );
};

export default DownloadAppButton;
