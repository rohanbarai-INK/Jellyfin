import React, { type CSSProperties } from 'react';

import AchievementOverlayContainer from './AchievementOverlayContainer';
import ActivityRewardContainer from './ActivityRewardContainer';
import CoinRewardOverlay from './CoinRewardOverlay';
import LevelUpOverlayManager from './LevelUpOverlayManager';
import RewardQueueManager from './RewardQueueManager';

const rewardSystemRootStyle: CSSProperties = {
    position: 'fixed',
    inset: 0,
    pointerEvents: 'none',
    zIndex: 5000
};

const activityContainerWrapperStyle: CSSProperties = {
    position: 'absolute',
    top: 'calc(env(safe-area-inset-top, 0px) + 1rem)',
    right: 'calc(env(safe-area-inset-right, 0px) + 1rem)'
};

const achievementContainerWrapperStyle: CSSProperties = {
    position: 'absolute',
    inset: 0,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    paddingTop: 'max(1rem, env(safe-area-inset-top, 0px))',
    paddingRight: 'max(1rem, env(safe-area-inset-right, 0px))',
    paddingBottom: 'max(1rem, env(safe-area-inset-bottom, 0px))',
    paddingLeft: 'max(1rem, env(safe-area-inset-left, 0px))'
};

const RewardSystemRoot = () => (
    <>
        <div style={rewardSystemRootStyle} aria-hidden>
            <RewardQueueManager />
            <CoinRewardOverlay />
            <div style={activityContainerWrapperStyle}>
                <ActivityRewardContainer />
            </div>
            <div style={achievementContainerWrapperStyle}>
                <AchievementOverlayContainer />
            </div>
        </div>
        <LevelUpOverlayManager />
    </>
);

export default RewardSystemRoot;
