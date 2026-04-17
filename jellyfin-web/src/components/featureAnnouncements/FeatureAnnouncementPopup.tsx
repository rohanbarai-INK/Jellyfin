import Dialog from '@mui/material/Dialog';
import React, { type FC, useMemo } from 'react';

import layoutManager from 'components/layoutManager';

import type { FeatureAnnouncementCampaign } from './featureAnnouncementTypes';

import './FeatureAnnouncementPopup.scss';

interface FeatureAnnouncementPopupProps {
    campaign: FeatureAnnouncementCampaign
    onCheckItOut: () => void
    onClose: () => void
    slideIndex?: number
    slideCount?: number
    onPreviousSlide?: () => void
    onNextSlide?: () => void
}

const FeatureAnnouncementPopup: FC<FeatureAnnouncementPopupProps> = ({
    campaign,
    onCheckItOut,
    onClose,
    slideIndex = 0,
    slideCount = 1,
    onPreviousSlide,
    onNextSlide
}) => {
    const popupClassName = useMemo(() => {
        if (layoutManager.tv) {
            return 'featureAnnouncementDialog tv';
        }

        if (layoutManager.mobile) {
            return 'featureAnnouncementDialog mobile';
        }

        return 'featureAnnouncementDialog';
    }, []);

    const heading = campaign.heading || "What's New?";
    const closeLabel = campaign.closeLabel || 'Close';
    const hasSlideNavigation = slideCount > 1;

    return (
        <Dialog
            open
            onClose={onClose}
            className={popupClassName}
            fullWidth
            maxWidth={false}
        >
            <div className='featureAnnouncementHeader'>
                <div>
                    <div className='featureAnnouncementEyebrowWrap'>
                        <div className='featureAnnouncementEyebrow'>{heading}</div>
                        {!!campaign.heroGifPath && (
                            <img
                                src={campaign.heroGifPath}
                                className='featureAnnouncementEyebrowGif'
                                alt=''
                                aria-hidden='true'
                            />
                        )}
                    </div>
                    <h2 className='featureAnnouncementTitle'>
                        {campaign.title}
                    </h2>
                    {!!campaign.subtitle && (
                        <p className='featureAnnouncementSubtitle'>{campaign.subtitle}</p>
                    )}
                </div>

                <button
                    type='button'
                    className='featureAnnouncementCloseButton'
                    onClick={onClose}
                    aria-label={closeLabel}
                >
                    ×
                </button>
            </div>

            <div className='featureAnnouncementBody'>
                <section className='featureAnnouncementMediaSection'>
                    {!!campaign.heroGifPath && (
                        <img
                            src={campaign.heroGifPath}
                            className='featureAnnouncementHeroGif'
                            alt='Feature announcement hero animation'
                        />
                    )}

                    {!!campaign.mediaAssets?.length && (
                        <div className='featureAnnouncementMediaGrid'>
                            {campaign.mediaAssets.map(asset => (
                                <figure key={`${campaign.id}-${asset.src}`} className='featureAnnouncementMediaCard'>
                                    <img
                                        src={asset.src}
                                        alt={asset.alt}
                                        className='featureAnnouncementMediaImage'
                                    />
                                    {!!asset.caption && (
                                        <figcaption className='featureAnnouncementMediaCaption'>
                                            {asset.caption}
                                        </figcaption>
                                    )}
                                </figure>
                            ))}
                        </div>
                    )}
                </section>

                <section className='featureAnnouncementContent'>
                    <p className='featureAnnouncementDescription'>{campaign.description}</p>

                    {!!campaign.highlights?.length && (
                        <ul className='featureAnnouncementHighlights'>
                            {campaign.highlights.map(highlight => (
                                <li key={`${campaign.id}-${highlight}`}>{highlight}</li>
                            ))}
                        </ul>
                    )}

                    {!!campaign.helpText && (
                        <p className='featureAnnouncementHelpText'>
                            {campaign.helpText}
                        </p>
                    )}

                    {hasSlideNavigation && (
                        <div className='featureAnnouncementSlideControls'>
                            <button
                                type='button'
                                className='featureAnnouncementSlideButton'
                                onClick={onPreviousSlide}
                                disabled={slideIndex <= 0}
                            >
                                Previous
                            </button>
                            <span className='featureAnnouncementSlideCount'>
                                {slideIndex + 1} / {slideCount}
                            </span>
                            <button
                                type='button'
                                className='featureAnnouncementSlideButton'
                                onClick={onNextSlide}
                                disabled={slideIndex >= slideCount - 1}
                            >
                                Next
                            </button>
                        </div>
                    )}

                    <div className='featureAnnouncementActions'>
                        <button
                            type='button'
                            className='featureAnnouncementPrimaryButton'
                            onClick={onCheckItOut}
                        >
                            {campaign.ctaLabel}
                        </button>
                        <button
                            type='button'
                            className='featureAnnouncementSecondaryButton'
                            onClick={onClose}
                        >
                            {closeLabel}
                        </button>
                    </div>
                </section>
            </div>
        </Dialog>
    );
};

export default FeatureAnnouncementPopup;
