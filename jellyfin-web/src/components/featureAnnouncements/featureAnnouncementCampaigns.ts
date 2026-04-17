import requestPopupAccentGif from 'assets/branding/request-popup-accent.gif';
import leaderboardAnnouncementPreview from 'assets/branding/leaderboard-announcement-preview.png';

import type { FeatureAnnouncementCampaign } from './featureAnnouncementTypes';

export const FEATURE_ANNOUNCEMENT_CAMPAIGNS: FeatureAnnouncementCampaign[] = [
    {
        id: 'leaderboard-launch-2026',
        enabled: true,
        heading: "What's New?",
        title: 'Leaderboard Is Here',
        subtitle: 'Track progress, compare stats, and climb the season rankings.',
        description: 'The new Leaderboard gives you a competitive view of your Jellyfin activity so you can measure progress and chase the next rank.',
        highlights: [
            'Explore season rankings across multiple metrics.',
            'Compare your progress against other members.',
            'See who is just ahead of you and who is right behind you.',
            'Open Achievements and switch to the Leaderboard tab.'
        ],
        helpText: 'Go to Achievements, then tap Leaderboard to start competing.',
        heroGifPath: requestPopupAccentGif,
        mediaAssets: [
            {
                src: leaderboardAnnouncementPreview,
                alt: 'Leaderboard feature preview screenshot',
                kind: 'image',
                caption: 'New leaderboard experience available in Achievements.'
            }
        ],
        ctaLabel: 'Check It Out',
        ctaRoute: '/achievements',
        ctaTargetType: 'internal',
        closeLabel: 'Close',
        startsAt: '2026-04-17T00:00:00.000Z',
        endsAt: '2026-04-30T23:59:59.000Z',
        maxImpressionsPerDay: 2,
        maxImpressionsPerUser: 10,
        priority: 100
    }
];
