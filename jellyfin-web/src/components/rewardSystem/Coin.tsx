import React from 'react';

interface CoinProps {
    className?: string;
}

const Coin = ({
    className = ''
}: CoinProps) => {
    return (
        <div className={`coinRewardCoin ${className}`}>
            <svg viewBox='0 0 100 100' className='coinRewardCoinSvg'>
                <circle cx='50' cy='50' r='45' fill='#fbbf24' stroke='#d97706' strokeWidth='5' />
                <circle cx='50' cy='50' r='35' fill='none' stroke='#f59e0b' strokeWidth='2' strokeDasharray='4 2' />
                <text x='50' y='60' fontSize='32' textAnchor='middle' fill='#78350f' fontWeight='700' fontFamily='sans-serif'>KF</text>
                <path d='M30 20 Q 50 10 70 20' stroke='white' strokeWidth='3' fill='none' opacity='0.6' />
            </svg>
        </div>
    );
};

export default Coin;
