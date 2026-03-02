import React, { useEffect, useMemo, useState } from 'react';

import Coin from './Coin';

interface FloatingItemProps {
    id: string;
    value: number;
    onComplete: (id: string) => void;
}

const SPARKLES = [
    { x: 30, y: -30, delayMs: 0 },
    { x: -30, y: 30, delayMs: 120 },
    { x: 35, y: 20, delayMs: 210 },
    { x: -30, y: -30, delayMs: 150 },
    { x: 0, y: -45, delayMs: 280 }
];

const FloatingItem = ({
    id,
    value,
    onComplete
}: FloatingItemProps) => {
    const [ showEffects, setShowEffects ] = useState(false);
    const [ isExiting, setIsExiting ] = useState(false);
    const sparkleSeed = useMemo(() => id.slice(-4), [id]);

    useEffect(() => {
        const spinTimer = window.setTimeout(() => {
            setShowEffects(true);
        }, 600);

        const exitTimer = window.setTimeout(() => {
            setIsExiting(true);
        }, 2200);

        const removeTimer = window.setTimeout(() => {
            onComplete(id);
        }, 2500);

        return () => {
            window.clearTimeout(spinTimer);
            window.clearTimeout(exitTimer);
            window.clearTimeout(removeTimer);
        };
    }, [ id, onComplete ]);

    return (
        <div className={`coinRewardFloatingItem ${isExiting ? 'coinRewardFloatingItem-exit' : ''}`}>
            <div className='coinRewardCoinColumn'>
                <div className={`coinRewardGlow ${showEffects ? 'coinRewardGlow-visible' : ''}`} />
                <div className='coinRewardSpinner'>
                    <Coin className='coinRewardCoinLarge' />
                </div>

                {showEffects && SPARKLES.map((sparkle) => (
                    <span
                        key={`${sparkleSeed}-${sparkle.x}-${sparkle.y}-${sparkle.delayMs}`}
                        className='coinRewardSparkle'
                        style={{
                            '--sparkle-x': `${sparkle.x}px`,
                            '--sparkle-y': `${sparkle.y}px`,
                            '--sparkle-delay': `${sparkle.delayMs}ms`
                        } as React.CSSProperties}
                    />
                ))}
            </div>

            <div className='coinRewardValue'>+{value}</div>
        </div>
    );
};

export default FloatingItem;
