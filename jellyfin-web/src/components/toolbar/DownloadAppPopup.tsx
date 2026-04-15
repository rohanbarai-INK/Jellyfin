import React, { FC, useState, useEffect } from 'react';

import step1Img from 'assets/install-guide/install-step1.png';
import step2Img from 'assets/install-guide/install-step2.png';
import step3Img from 'assets/install-guide/install-step3.png';
import step4Img from 'assets/install-guide/install-step4.png';

interface DownloadAppPopupProps {
    onClose: () => void;
}

const steps = [
    {
        number: 1,
        title: 'Download the APK',
        desc: 'The APK file has started downloading to your device. Check your browser\'s notification bar or the Downloads folder.',
        icon: '⬇️',
        image: step1Img,
        tip: 'Look for \'KnightFlix.apk\' in your notification shade',
        color: 'linear-gradient(to right, #3b82f6, #06b6d4)',
        barColor: '#3b82f6'
    },
    {
        number: 2,
        title: 'Allow Unknown Sources',
        desc: 'Go to Settings → Security → "Install unknown apps" and toggle ON for your browser or file manager.',
        icon: '🔓',
        image: step2Img,
        tip: 'You only need to do this once on your device',
        color: 'linear-gradient(to right, #f97316, #f59e0b)',
        barColor: '#f97316'
    },
    {
        number: 3,
        title: 'Open & Install the APK',
        desc: 'Tap the downloaded APK file from your notifications or Downloads folder. Hit "Install" on the prompt.',
        icon: '📲',
        image: step3Img,
        tip: 'If prompted, tap \'More details\' then \'Install anyway\'',
        color: 'linear-gradient(to right, #8b5cf6, #a855f7)',
        barColor: '#8b5cf6'
    },
    {
        number: 4,
        title: 'Open KnightFlix 🎉',
        desc: 'Installation complete! Tap \'Open\' to launch the app and start streaming your favourite content.',
        icon: '🚀',
        image: step4Img,
        tip: 'Log in with your existing KnightFlix credentials',
        color: 'linear-gradient(to right, #22c55e, #10b981)',
        barColor: '#22c55e'
    }
];

const DownloadAppPopup: FC<DownloadAppPopupProps> = ({ onClose }) => {
    const [activeStep, setActiveStep] = useState(0);
    const step = steps[activeStep];
    const isLast = activeStep === steps.length - 1;
    const isFirst = activeStep === 0;

    // Close on ESC
    useEffect(() => {
        const handler = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
        document.addEventListener('keydown', handler);
        return () => document.removeEventListener('keydown', handler);
    }, [onClose]);

    // Prevent background scroll
    useEffect(() => {
        document.body.style.overflow = 'hidden';
        return () => { document.body.style.overflow = ''; };
    }, []);

    return (
        <div
            style={{
                position: 'fixed',
                inset: 0,
                zIndex: 1500,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: '16px',
                animation: 'kfFadeIn 0.2s ease both'
            }}
        >
            {/* Backdrop */}
            <div
                onClick={onClose}
                style={{
                    position: 'absolute',
                    inset: 0,
                    background: 'rgba(0,0,0,0.75)',
                    backdropFilter: 'blur(4px)'
                }}
            />

            {/* Modal */}
            <div
                style={{
                    position: 'relative',
                    width: '100%',
                    maxWidth: '640px',
                    background: '#16161f',
                    borderRadius: '24px',
                    border: '1px solid rgba(255,255,255,0.1)',
                    boxShadow: '0 25px 80px rgba(0,0,0,0.7)',
                    overflow: 'hidden',
                    animation: 'kfPopupIn 0.28s cubic-bezier(.21,1.02,.73,1) both'
                }}
            >
                {/* Top color bar */}
                <div style={{
                    height: '6px',
                    background: step.color,
                    width: '100%',
                    transition: 'background 0.5s'
                }} />

                {/* Close button */}
                <button
                    onClick={onClose}
                    title='Close'
                    style={{
                        position: 'absolute',
                        top: '16px',
                        right: '16px',
                        zIndex: 10,
                        width: '36px',
                        height: '36px',
                        borderRadius: '50%',
                        background: 'rgba(255,255,255,0.05)',
                        border: '1px solid rgba(255,255,255,0.1)',
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: 'rgba(255,255,255,0.6)',
                        transition: 'background 0.15s, color 0.15s, transform 0.2s'
                    }}
                    onMouseEnter={e => {
                        const el = e.currentTarget as HTMLButtonElement;
                        el.style.background = 'rgba(255,255,255,0.15)';
                        el.style.color = '#fff';
                        el.style.transform = 'rotate(90deg)';
                    }}
                    onMouseLeave={e => {
                        const el = e.currentTarget as HTMLButtonElement;
                        el.style.background = 'rgba(255,255,255,0.05)';
                        el.style.color = 'rgba(255,255,255,0.6)';
                        el.style.transform = 'rotate(0deg)';
                    }}
                >
                    <svg style={{ width: '16px', height: '16px' }} fill='none' stroke='currentColor' strokeWidth={2.5} viewBox='0 0 24 24'>
                        <path d='M18 6 6 18M6 6l12 12' strokeLinecap='round' strokeLinejoin='round' />
                    </svg>
                </button>

                {/* Header */}
                <div style={{
                    padding: '20px 24px 16px',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '12px'
                }}>
                    <div style={{
                        width: '44px',
                        height: '44px',
                        borderRadius: '14px',
                        background: step.color,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontSize: '20px',
                        flexShrink: 0,
                        transition: 'background 0.3s',
                        boxShadow: '0 4px 16px rgba(0,0,0,0.3)'
                    }}>
                        {step.icon}
                    </div>
                    <div>
                        <h2 style={{ margin: 0, color: '#fff', fontWeight: 700, fontSize: '18px', lineHeight: 1.3 }}>
                            How to Install KnightFlix
                        </h2>
                        <p style={{ margin: 0, color: 'rgba(255,255,255,0.4)', fontSize: '12px', marginTop: '2px' }}>
                            Android APK · Side-load Installation Guide
                        </p>
                    </div>
                </div>

                {/* Step progress bars */}
                <div style={{ padding: '0 24px 16px' }}>
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-start' }}>
                        {steps.map((s, i) => (
                            <button
                                key={i}
                                onClick={() => setActiveStep(i)}
                                style={{
                                    flex: 1,
                                    display: 'flex',
                                    flexDirection: 'column',
                                    alignItems: 'center',
                                    gap: '4px',
                                    background: 'none',
                                    border: 'none',
                                    cursor: 'pointer',
                                    padding: 0
                                }}
                            >
                                <div style={{
                                    width: '100%',
                                    height: '6px',
                                    borderRadius: '999px',
                                    transition: 'background 0.4s',
                                    background: i < activeStep
                                        ? '#22c55e'
                                        : i === activeStep
                                            ? step.barColor
                                            : 'rgba(255,255,255,0.1)'
                                }} />
                                <span style={{
                                    fontSize: '9px',
                                    fontWeight: 600,
                                    color: i === activeStep ? 'rgba(255,255,255,0.8)' : 'rgba(255,255,255,0.3)'
                                }}>
                                    Step {s.number}
                                </span>
                            </button>
                        ))}
                    </div>
                </div>

                {/* Content */}
                <div style={{ padding: '0 24px 24px' }}>
                    <div style={{ display: 'flex', gap: '20px', alignItems: 'flex-start' }}>
                        {/* Step icon panel (replaces phone screenshot) */}
                        <div style={{
                            flexShrink: 0,
                            width: '120px',
                            display: 'none'
                        }}
                            className='kf-popup-screenshot'
                        >
                            <div style={{
                                borderRadius: '16px',
                                overflow: 'hidden',
                                border: '1px solid rgba(255,255,255,0.1)',
                                aspectRatio: '9/16',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                flexDirection: 'column',
                                gap: '8px',
                                background: step.color
                            }}>
                                <img
                                    key={step.number}
                                    src={step.image}
                                    alt={`Step ${step.number}`}
                                    style={{ width: '100%', height: '100%', objectFit: 'cover', animation: 'kfImgFadeIn 0.35s ease both' }}
                                />
                            </div>
                            <div style={{
                                marginTop: '8px',
                                textAlign: 'center',
                                padding: '4px 12px',
                                borderRadius: '999px',
                                fontSize: '12px',
                                fontWeight: 700,
                                color: '#fff',
                                background: step.color,
                                display: 'inline-block',
                                width: 'fit-content',
                                marginLeft: 'auto',
                                marginRight: 'auto'
                            }}>
                                {step.number} / {steps.length}
                            </div>
                        </div>

                        {/* Text content */}
                        <div
                            key={activeStep}
                            style={{
                                flex: 1,
                                animation: 'kfSlideUp 0.3s ease both'
                            }}
                        >
                            <h3 style={{
                                margin: '0 0 8px',
                                color: '#fff',
                                fontWeight: 700,
                                fontSize: '20px',
                                lineHeight: 1.3
                            }}>
                                {step.title}
                            </h3>
                            <p style={{
                                margin: 0,
                                color: 'rgba(255,255,255,0.6)',
                                fontSize: '14px',
                                lineHeight: 1.6
                            }}>
                                {step.desc}
                            </p>

                            {/* Pro tip */}
                            <div style={{
                                marginTop: '16px',
                                display: 'flex',
                                alignItems: 'flex-start',
                                gap: '10px',
                                padding: '12px',
                                borderRadius: '12px',
                                background: 'rgba(255,255,255,0.05)',
                                border: '1px solid rgba(255,255,255,0.08)'
                            }}>
                                <span style={{ fontSize: '16px', marginTop: '1px', flexShrink: 0 }}>💡</span>
                                <p style={{ margin: 0, color: 'rgba(255,255,255,0.5)', fontSize: '12px', lineHeight: 1.6 }}>
                                    <span style={{ color: 'rgba(255,255,255,0.7)', fontWeight: 600 }}>Pro tip: </span>
                                    {step.tip}
                                </p>
                            </div>

                            {/* All steps mini list */}
                            <div style={{ marginTop: '16px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
                                {steps.map((s, i) => (
                                    <button
                                        key={i}
                                        onClick={() => setActiveStep(i)}
                                        style={{
                                            width: '100%',
                                            display: 'flex',
                                            alignItems: 'center',
                                            gap: '12px',
                                            padding: '10px',
                                            borderRadius: '12px',
                                            textAlign: 'left',
                                            cursor: 'pointer',
                                            background: i === activeStep ? 'rgba(255,255,255,0.06)' : 'transparent',
                                            border: i === activeStep ? '1px solid rgba(255,255,255,0.12)' : '1px solid transparent',
                                            transition: 'background 0.2s, border-color 0.2s'
                                        }}
                                        onMouseEnter={e => {
                                            if (i !== activeStep) {
                                                (e.currentTarget as HTMLButtonElement).style.background = 'rgba(255,255,255,0.04)';
                                            }
                                        }}
                                        onMouseLeave={e => {
                                            if (i !== activeStep) {
                                                (e.currentTarget as HTMLButtonElement).style.background = 'transparent';
                                            }
                                        }}
                                    >
                                        <div style={{
                                            width: '28px',
                                            height: '28px',
                                            borderRadius: '50%',
                                            display: 'flex',
                                            alignItems: 'center',
                                            justifyContent: 'center',
                                            fontSize: '12px',
                                            fontWeight: 700,
                                            flexShrink: 0,
                                            background: i < activeStep
                                                ? '#22c55e'
                                                : i === activeStep
                                                    ? step.color
                                                    : 'rgba(255,255,255,0.1)',
                                            color: i < activeStep || i === activeStep ? '#fff' : 'rgba(255,255,255,0.4)',
                                            transition: 'background 0.2s'
                                        }}>
                                            {i < activeStep ? (
                                                <svg style={{ width: '14px', height: '14px' }} fill='none' stroke='currentColor' strokeWidth={3} viewBox='0 0 24 24'>
                                                    <path d='M20 6 9 17l-5-5' strokeLinecap='round' strokeLinejoin='round' />
                                                </svg>
                                            ) : s.number}
                                        </div>
                                        <p style={{
                                            margin: 0,
                                            fontSize: '12px',
                                            fontWeight: 600,
                                            flex: 1,
                                            color: i === activeStep ? '#fff' : 'rgba(255,255,255,0.4)'
                                        }}>
                                            {s.title}
                                        </p>
                                        <span style={{ fontSize: '16px' }}>{s.icon}</span>
                                    </button>
                                ))}
                            </div>
                        </div>
                    </div>

                    {/* Navigation buttons */}
                    <div style={{ display: 'flex', gap: '12px', marginTop: '20px' }}>
                        <button
                            onClick={() => setActiveStep(p => Math.max(0, p - 1))}
                            disabled={isFirst}
                            style={{
                                flex: 1,
                                padding: '10px 0',
                                borderRadius: '12px',
                                border: '1px solid rgba(255,255,255,0.1)',
                                background: 'transparent',
                                color: isFirst ? 'rgba(255,255,255,0.2)' : 'rgba(255,255,255,0.6)',
                                fontSize: '14px',
                                fontWeight: 600,
                                cursor: isFirst ? 'not-allowed' : 'pointer',
                                transition: 'background 0.15s, color 0.15s'
                            }}
                            onMouseEnter={e => {
                                if (!isFirst) {
                                    (e.currentTarget as HTMLButtonElement).style.background = 'rgba(255,255,255,0.05)';
                                    (e.currentTarget as HTMLButtonElement).style.color = '#fff';
                                }
                            }}
                            onMouseLeave={e => {
                                if (!isFirst) {
                                    (e.currentTarget as HTMLButtonElement).style.background = 'transparent';
                                    (e.currentTarget as HTMLButtonElement).style.color = 'rgba(255,255,255,0.6)';
                                }
                            }}
                        >
                            ← Previous
                        </button>

                        {isLast ? (
                            <button
                                onClick={onClose}
                                style={{
                                    flex: 1,
                                    padding: '10px 0',
                                    borderRadius: '12px',
                                    border: 'none',
                                    background: 'linear-gradient(to right, #22c55e, #10b981)',
                                    color: '#fff',
                                    fontWeight: 700,
                                    fontSize: '14px',
                                    cursor: 'pointer',
                                    boxShadow: '0 4px 16px rgba(34,197,94,0.3)',
                                    transition: 'transform 0.15s'
                                }}
                                onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.02)'; }}
                                onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)'; }}
                            >
                                🎉 Done — Start Watching!
                            </button>
                        ) : (
                            <button
                                onClick={() => setActiveStep(p => Math.min(steps.length - 1, p + 1))}
                                style={{
                                    flex: 1,
                                    padding: '10px 0',
                                    borderRadius: '12px',
                                    border: 'none',
                                    background: step.color,
                                    color: '#fff',
                                    fontWeight: 700,
                                    fontSize: '14px',
                                    cursor: 'pointer',
                                    boxShadow: '0 4px 16px rgba(0,0,0,0.3)',
                                    transition: 'transform 0.15s'
                                }}
                                onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.02)'; }}
                                onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)'; }}
                            >
                                Next Step →
                            </button>
                        )}
                    </div>
                </div>
            </div>

            <style>{`
                @keyframes kfFadeIn {
                    from { opacity: 0; }
                    to   { opacity: 1; }
                }
                @keyframes kfPopupIn {
                    from { opacity: 0; transform: scale(0.93) translateY(20px); }
                    to   { opacity: 1; transform: scale(1) translateY(0); }
                }
                @keyframes kfSlideUp {
                    from { opacity: 0; transform: translateY(12px); }
                    to   { opacity: 1; transform: translateY(0); }
                }
                @keyframes kfImgFadeIn {
                    from { opacity: 0; transform: scale(1.04); }
                    to   { opacity: 1; transform: scale(1); }
                }
                @media (min-width: 480px) {
                    .kf-popup-screenshot {
                        display: block !important;
                    }
                }
            `}</style>
        </div>
    );
};

export default DownloadAppPopup;
