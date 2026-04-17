export type FeatureAnnouncementMediaKind = 'image' | 'gif';
export type FeatureAnnouncementCtaTargetType = 'internal' | 'external';

export interface FeatureAnnouncementMediaAsset {
    src: string
    alt: string
    caption?: string
    kind?: FeatureAnnouncementMediaKind
}

export interface FeatureAnnouncementCampaign {
    id: string
    enabled: boolean
    heading?: string
    title: string
    subtitle?: string
    description: string
    highlights?: string[]
    helpText?: string
    heroGifPath?: string
    mediaAssets?: FeatureAnnouncementMediaAsset[]
    ctaLabel: string
    ctaRoute: string
    ctaTargetType?: FeatureAnnouncementCtaTargetType
    closeLabel?: string
    startsAt?: string
    endsAt?: string
    maxImpressionsPerDay?: number
    maxImpressionsPerUser: number
    priority?: number
    sortOrder?: number
}
