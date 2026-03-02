export declare global {
    import { ApiClient, Events } from 'jellyfin-apiclient';
    import { RewardPayload, RewardSystemPublicApi } from 'components/rewardSystem/RewardSystem';
    import { ShowAchievementOptions } from 'components/rewardSystem/AchievementOverlayMount';
    import { AchievementUnlockResult, UserAchievementRow } from 'utils/achievementsApi';

    interface Window {
        ApiClient: ApiClient;
        Events: Events;
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        NativeShell: any;
        Loading: {
            show(type?: 'system' | 'media'): void;
            hide(): void;
        };
        RewardSystem?: RewardSystemPublicApi;
        showAchievement?: (options: ShowAchievementOptions) => void;
        unlockAchievement?: (achievementId: string) => Promise<AchievementUnlockResult>;
        syncAchievements?: () => Promise<UserAchievementRow[]>;
        // eslint-disable-next-line @typescript-eslint/naming-convention
        __triggerRewardTest?: (payload: RewardPayload) => void;
    }

    interface DocumentEventMap {
        'viewshow': CustomEvent;
    }

    const __COMMIT_SHA__: string;
    const __JF_BUILD_VERSION__: string;
    const __PACKAGE_JSON_NAME__: string;
    const __PACKAGE_JSON_VERSION__: string;
    const __USE_SYSTEM_FONTS__: boolean;
    const __WEBPACK_SERVE__: boolean;
}
