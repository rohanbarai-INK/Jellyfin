import React, { useEffect, useRef, useState } from 'react';
import confetti from 'canvas-confetti';
import { AnimatePresence, motion } from 'framer-motion';

import './levelUpOverlay.scss';

interface LevelUpOverlayProps {
    isOpen: boolean;
    level: number;
    previousLevel: number;
    rewardCoins: number;
    quote: string;
    onClaim: () => void;
    onDismiss?: () => void;
}

const overlaySpring = {
    type: 'spring',
    stiffness: 280,
    damping: 24
} as const;

const LevelUpOverlay = ({
    isOpen,
    level,
    previousLevel,
    rewardCoins,
    quote,
    onClaim,
    onDismiss
}: LevelUpOverlayProps) => {
    const cardRef = useRef<HTMLDivElement | null>(null);
    const [needsScroll, setNeedsScroll] = useState(false);
    const isMilestoneLevel = level % 10 === 0;
    const dismissOverlay = onDismiss || onClaim;

    useEffect(() => {
        if (!isOpen) {
            return;
        }

        confetti({
            particleCount: 120,
            spread: 92,
            startVelocity: 44,
            origin: { y: 0.64 },
            colors: ['#facc15', '#22d3ee', '#f9fafb']
        });

        const burstTimer = window.setTimeout(() => {
            confetti({
                particleCount: 90,
                spread: 120,
                startVelocity: 38,
                origin: { y: 0.52 },
                colors: ['#fde68a', '#67e8f9', '#e2e8f0']
            });
        }, 240);

        return () => {
            window.clearTimeout(burstTimer);
        };
    }, [isOpen]);

    useEffect(() => {
        if (!isOpen || isMilestoneLevel) {
            return;
        }

        const autoDismissTimer = window.setTimeout(() => {
            dismissOverlay();
        }, 5000);

        return () => {
            window.clearTimeout(autoDismissTimer);
        };
    }, [dismissOverlay, isMilestoneLevel, isOpen]);

    useEffect(() => {
        if (!isOpen) {
            return;
        }

        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === 'Escape') {
                dismissOverlay();
            }
        };

        window.addEventListener('keydown', handleKeyDown);

        return () => {
            window.removeEventListener('keydown', handleKeyDown);
        };
    }, [dismissOverlay, isOpen]);

    useEffect(() => {
        if (!isOpen) {
            setNeedsScroll(false);
            return;
        }

        const checkNeedsScroll = () => {
            const card = cardRef.current;
            if (!card) {
                return;
            }

            setNeedsScroll(card.scrollHeight > card.clientHeight + 1);
        };

        const firstMeasure = window.requestAnimationFrame(checkNeedsScroll);
        window.addEventListener('resize', checkNeedsScroll);

        let resizeObserver: ResizeObserver | null = null;
        if (typeof ResizeObserver !== 'undefined' && cardRef.current) {
            resizeObserver = new ResizeObserver(checkNeedsScroll);
            resizeObserver.observe(cardRef.current);
        }

        return () => {
            window.cancelAnimationFrame(firstMeasure);
            window.removeEventListener('resize', checkNeedsScroll);
            resizeObserver?.disconnect();
        };
    }, [isMilestoneLevel, isOpen, level, quote, rewardCoins]);

    return (
        <AnimatePresence>
            {isOpen && (
                <motion.div
                    className='levelUpOverlayBackdrop'
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    exit={{ opacity: 0 }}
                >
                    <motion.div
                        ref={cardRef}
                        className={`levelUpOverlayCard${needsScroll ? ' levelUpOverlayCard-scrollable' : ''}`}
                        initial={{ opacity: 0, y: 24, scale: 0.87 }}
                        animate={{ opacity: 1, y: 0, scale: 1 }}
                        exit={{ opacity: 0, y: 16, scale: 0.92 }}
                        transition={overlaySpring}
                        role='dialog'
                        aria-modal='true'
                        aria-label={`Level up to level ${level}`}
                        onClick={(event) => {
                            event.stopPropagation();
                        }}
                    >
                        <div className='levelUpOverlayGlow levelUpOverlayGlow-primary' />
                        <div className='levelUpOverlayGlow levelUpOverlayGlow-secondary' />

                        <div className='levelUpOverlayContent'>
                            <div className='levelUpOverlayEyebrow'>Level Up</div>

                            <div className='levelUpOverlayLevelRow'>
                                <span className='levelUpOverlayLevelFrom'>Lv {previousLevel}</span>
                                <span className='levelUpOverlayArrow' aria-hidden='true'>&rarr;</span>
                                <span className='levelUpOverlayLevelTo'>{level}</span>
                            </div>

                            {isMilestoneLevel ? (
                                <motion.div
                                    className='levelUpOverlayMilestone'
                                    initial={{ opacity: 0, y: 10 }}
                                    animate={{ opacity: 1, y: 0 }}
                                    transition={{ delay: 0.16, duration: 0.36, ease: 'easeOut' }}
                                >
                                    <div className='levelUpOverlayMilestoneLabel'>Milestone Reached</div>
                                    <motion.div
                                        className='levelUpOverlayMilestoneCoins'
                                        animate={{ scale: [1, 1.06, 1] }}
                                        transition={{ duration: 1.1, repeat: Infinity }}
                                    >
                                        {'\uD83D\uDCB0'} {Math.max(0, rewardCoins).toLocaleString()} COINS
                                    </motion.div>
                                </motion.div>
                            ) : (
                                <motion.div
                                    className='levelUpOverlayQuote'
                                    initial={{ opacity: 0, y: 10 }}
                                    animate={{ opacity: 1, y: 0 }}
                                    transition={{ delay: 0.16, duration: 0.36, ease: 'easeOut' }}
                                >
                                    <div className='levelUpOverlayQuoteLabel'>Level {level}</div>
                                    <p>{quote}</p>
                                </motion.div>
                            )}

                            {isMilestoneLevel ? (
                                <button
                                    type='button'
                                    className='levelUpOverlayClaimButton'
                                    onClick={onClaim}
                                >
                                    CLAIM
                                </button>
                            ) : (
                                <>
                                    <div className='levelUpOverlayAutoDismissText'>Auto dismiss in 5s</div>
                                    <button
                                        type='button'
                                        className='levelUpOverlayDismissButton'
                                        onClick={dismissOverlay}
                                    >
                                        DISMISS
                                    </button>
                                </>
                            )}
                        </div>
                    </motion.div>
                </motion.div>
            )}
        </AnimatePresence>
    );
};

export default LevelUpOverlay;
