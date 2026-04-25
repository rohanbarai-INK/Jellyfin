import { ImageType } from '@jellyfin/sdk/lib/generated-client/models/image-type';
import type { BaseItemDto } from '@jellyfin/sdk/lib/generated-client/models/base-item-dto';
import type { UserDto } from '@jellyfin/sdk/lib/generated-client/models/user-dto';
import type { ApiClient } from 'jellyfin-apiclient';

import { playbackManager } from 'components/playback/playbackmanager';
import { appRouter } from 'components/router/appRouter';
import { getItemBackdropImageUrl } from 'utils/jellyfin-apiclient/backdropImage';
import { getTrendingNow, getTrendingNowSettings, type TrendingNowRailItem } from 'utils/trendingNowApi';

import type { SectionOptions } from './section';

import './trendingNow.scss';

const PERIOD = 'week';
const DEFAULT_RAIL_LIMIT = 12;
const MIN_RAIL_LIMIT = 1;
const MAX_RAIL_LIMIT = 30;
const AUTO_ROTATE_MS = 7000;
const MARKETING_ROTATION_STORAGE_KEY = 'knightflixTrendingMarketingRotation';
const MARKETING_LINES = [
    "Tonight's top picks are ready. Press play and dive in.",
    'Fresh buzz, big stories, and the titles everyone is talking about.',
    "Your next binge starts here with what's hot right now.",
    'Trending now: fan favorites and rising hits in one spotlight.',
    "Discover the week's most watched stories and jump in instantly."
];

type RailState = {
    responseLabel: string;
    items: Array<{
        contentItem: BaseItemDto;
        trendingItem: TrendingNowRailItem;
        posterUrl: string;
        backdropUrl: string;
        metadataLine: string;
    }>;
};

type TrendingNowSectionElement = HTMLElement & {
    _trendingRotationTimer?: number;
};

const escapeHtml = (value: string) => value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');

const createShuffledOrder = (size: number) => {
    const order = Array.from({ length: size }, (_, index) => index);
    for (let index = order.length - 1; index > 0; index -= 1) {
        const randomIndex = Math.floor(Math.random() * (index + 1));
        [ order[index], order[randomIndex] ] = [ order[randomIndex], order[index] ];
    }

    return order;
};

const getRotatingMarketingLine = () => {
    if (!MARKETING_LINES.length) {
        return 'Trending stories picked for your next watch.';
    }

    try {
        const raw = window.localStorage.getItem(MARKETING_ROTATION_STORAGE_KEY);
        const parsed = raw ? JSON.parse(raw) as { order?: number[]; pointer?: number } : {};
        const validOrder = Array.isArray(parsed.order)
            && parsed.order.length === MARKETING_LINES.length
            && parsed.order.every(item => Number.isInteger(item) && item >= 0 && item < MARKETING_LINES.length);
        const order: number[] = validOrder
            ? parsed.order as number[]
            : createShuffledOrder(MARKETING_LINES.length);
        const pointer = Number.isInteger(parsed.pointer) ? parsed.pointer as number : 0;
        const safePointer = Math.max(0, Math.min(pointer, order.length - 1));
        const lineIndex = order[safePointer];
        const nextPointer = safePointer + 1 >= order.length ? 0 : safePointer + 1;
        const nextOrder = nextPointer === 0 ? createShuffledOrder(MARKETING_LINES.length) : order;

        window.localStorage.setItem(MARKETING_ROTATION_STORAGE_KEY, JSON.stringify({
            order: nextOrder,
            pointer: nextPointer
        }));

        return MARKETING_LINES[lineIndex] || MARKETING_LINES[0];
    } catch (error) {
        console.warn('[TrendingNowHomeSection] marketing line rotation fallback', error);
        return MARKETING_LINES[Math.floor(Math.random() * MARKETING_LINES.length)];
    }
};

const wrapIndex = (index: number, size: number) => {
    if (size <= 0) {
        return 0;
    }

    return (index + size) % size;
};

const formatRuntime = (ticks: number | null) => {
    if (!ticks || ticks <= 0) {
        return '';
    }

    const minutes = Math.max(1, Math.round(ticks / 600000000));
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;

    if (!hours) {
        return `${minutes}m`;
    }

    return remainingMinutes ? `${hours}h ${remainingMinutes}m` : `${hours}h`;
};

const buildMetadataLine = (contentItem: BaseItemDto, trendingItem: TrendingNowRailItem) => {
    const parts = [
        contentItem.ProductionYear || trendingItem.productionYear || null,
        trendingItem.officialRating || null,
        formatRuntime(trendingItem.runTimeTicks),
        trendingItem.genres[0] || null
    ].filter(Boolean);

    return parts.join(' | ');
};

const getPrimaryImageUrl = (apiClient: ApiClient, item: BaseItemDto) => {
    const imageItem = item as BaseItemDto & {
        PrimaryImageTag?: string;
        PrimaryImageItemId?: string;
        SeriesPrimaryImageTag?: string;
        SeriesId?: string;
        ParentPrimaryImageTag?: string;
        ParentPrimaryImageItemId?: string;
    };

    if (imageItem.ImageTags?.Primary && imageItem.Id) {
        return apiClient.getScaledImageUrl(imageItem.Id, {
            type: ImageType.Primary,
            tag: imageItem.ImageTags.Primary,
            height: 640
        });
    }

    if (imageItem.PrimaryImageTag) {
        return apiClient.getScaledImageUrl(imageItem.PrimaryImageItemId || imageItem.Id || '', {
            type: ImageType.Primary,
            tag: imageItem.PrimaryImageTag,
            height: 640
        });
    }

    if (imageItem.SeriesPrimaryImageTag && imageItem.SeriesId) {
        return apiClient.getScaledImageUrl(imageItem.SeriesId, {
            type: ImageType.Primary,
            tag: imageItem.SeriesPrimaryImageTag,
            height: 640
        });
    }

    if (imageItem.ParentPrimaryImageTag && imageItem.ParentPrimaryImageItemId) {
        return apiClient.getScaledImageUrl(imageItem.ParentPrimaryImageItemId, {
            type: ImageType.Primary,
            tag: imageItem.ParentPrimaryImageTag,
            height: 640
        });
    }

    return '';
};

async function loadRailState(apiClient: ApiClient, userId: string): Promise<RailState> {
    let railLimit = DEFAULT_RAIL_LIMIT;
    try {
        const settings = await getTrendingNowSettings(apiClient);
        const parsed = Number(settings.maxSlides);
        if (Number.isFinite(parsed)) {
            railLimit = Math.min(MAX_RAIL_LIMIT, Math.max(MIN_RAIL_LIMIT, Math.round(parsed)));
        }
    } catch (error) {
        console.warn('[TrendingNowHomeSection] failed to load settings, using default rail limit', error);
    }

    const response = await getTrendingNow(PERIOD, railLimit, apiClient);
    const itemIds = response.items
        .map(item => item.itemId)
        .filter(Boolean);

    if (!itemIds.length) {
        return {
            responseLabel: response.periodLabel,
            items: []
        };
    }

    const result = await apiClient.getItems(userId, {
        Ids: itemIds.join(','),
        Limit: itemIds.length,
        Recursive: true,
        Fields: 'PrimaryImageAspectRatio,ProductionYear,Overview,Genres,OfficialRating,RunTimeTicks',
        ImageTypeLimit: 2,
        EnableImageTypes: 'Primary,Thumb,Backdrop',
        EnableTotalRecordCount: false
    });

    const itemsById = new Map((result.Items || []).map(item => [ item.Id, item ]));
    const enrichedItems = response.items
        .map(trendingItem => {
            const contentItem = itemsById.get(trendingItem.itemId);
            if (!contentItem?.Id) {
                return null;
            }

            return {
                contentItem,
                trendingItem,
                posterUrl: getPrimaryImageUrl(apiClient, contentItem),
                backdropUrl: getItemBackdropImageUrl(apiClient, contentItem, { width: 1600, quality: 90 }) || '',
                metadataLine: buildMetadataLine(contentItem, trendingItem)
            };
        })
        .filter((item): item is RailState['items'][number] => Boolean(item));

    return {
        responseLabel: response.periodLabel,
        items: enrichedItems
    };
}

function getHeroOverview(item: RailState['items'][number]) {
    const overview = (item.contentItem.Overview || item.trendingItem.overview || '').trim();
    if (!overview) {
        return item.trendingItem.explanationText || item.trendingItem.contextText;
    }

    return overview.length > 210 ? `${overview.slice(0, 207).trim()}...` : overview;
}

function renderRail(
    elem: TrendingNowSectionElement,
    state: RailState,
    activeIndex: number,
    apiClient: ApiClient,
    marketingLine: string,
    onPrevious: () => void,
    onNext: () => void
) {
    const safeIndex = wrapIndex(activeIndex, state.items.length);
    const active = state.items[safeIndex];
    const backgroundImage = active.backdropUrl || active.posterUrl;
    const badges = [
        active.trendingItem.primaryLabel,
        active.trendingItem.secondaryLabel
    ].filter(Boolean);

    elem.innerHTML = `
        <section class="trendingNowHomeSection" aria-label="Trending Now">
            <div class="sectionTitleContainer sectionTitleContainer-cards padded-left padded-right trendingNowHomeHeader">
                <div class="trendingNowHomeHeaderCopy">
                    <h2 class="sectionTitle sectionTitle-cards">Trending Now</h2>
                    <div class="trendingNowHomeSubtitle">${escapeHtml(marketingLine)}</div>
                </div>
                <div class="trendingNowHomePeriodBadge">${escapeHtml(state.responseLabel)}</div>
            </div>
            <div class="trendingNowHomeStatus hide"></div>
            <div class="trendingNowHomeHero padded-left padded-right">
                <div class="trendingNowHomeHeroBackdrop"${backgroundImage ? ` style="background-image: url('${escapeHtml(backgroundImage)}')"` : ''}></div>
                <div class="trendingNowHomeHeroOverlay"></div>
                <div class="trendingNowHomeHeroContent">
                    <div class="trendingNowHomeHeroMedia">
                        ${active.posterUrl ? `<img class="trendingNowHomeHeroPoster" src="${escapeHtml(active.posterUrl)}" alt="${escapeHtml(active.trendingItem.title)} poster" />` : '<div class="trendingNowHomeHeroPoster trendingNowHomeHeroPosterFallback">KN</div>'}
                    </div>
                    <div class="trendingNowHomeHeroInfo">
                        <div class="trendingNowHomeBadgeRow">
                            ${badges.map(badge => `<span class="trendingNowHomeBadge">${escapeHtml(badge)}</span>`).join('')}
                        </div>
                        <h3 class="trendingNowHomeHeroTitle">${escapeHtml(active.trendingItem.title)}</h3>
                        <div class="trendingNowHomeHeroMeta">${escapeHtml(active.metadataLine || active.trendingItem.contextText)}</div>
                        <p class="trendingNowHomeHeroOverview">${escapeHtml(getHeroOverview(active))}</p>
                        <div class="trendingNowHomeHeroReasons">
                            <span class="trendingNowHomeReason">${escapeHtml(active.trendingItem.explanationText || active.trendingItem.primaryLabel)}</span>
                            <span class="trendingNowHomeReason subtle">${escapeHtml(active.trendingItem.tagline || active.trendingItem.contextText || `Watched by ${active.trendingItem.uniqueViewers} users`)}</span>
                        </div>
                        <div class="trendingNowHomeActions">
                            <button type="button" class="emby-button raised button-submit trendingNowHomeActionPlay" data-action="play" data-itemid="${escapeHtml(active.contentItem.Id || '')}">
                                Play
                            </button>
                            <button type="button" class="emby-button button-flat trendingNowHomeActionInfo" data-action="info" data-itemid="${escapeHtml(active.contentItem.Id || '')}">
                                More Info
                            </button>
                        </div>
                    </div>
                </div>
                <button type="button" class="trendingNowHomeNavButton prev" data-nav="prev" aria-label="Previous trending title">
                    <span aria-hidden="true">&#x2039;</span>
                </button>
                <button type="button" class="trendingNowHomeNavButton next" data-nav="next" aria-label="Next trending title">
                    <span aria-hidden="true">&#x203A;</span>
                </button>
            </div>
        </section>
    `;

    elem.querySelectorAll<HTMLButtonElement>('.trendingNowHomeNavButton').forEach(button => {
        button.addEventListener('click', () => {
            if (button.dataset.nav === 'prev') {
                onPrevious();
                return;
            }

            onNext();
        });
    });

    elem.querySelectorAll<HTMLButtonElement>('[data-action]').forEach(button => {
        button.addEventListener('click', async () => {
            const itemId = button.dataset.itemid || '';
            if (!itemId) {
                return;
            }

            if (button.dataset.action === 'play') {
                const item = await apiClient.getItem(apiClient.getCurrentUserId(), itemId);
                playbackManager.play({
                    items: [item]
                });
                return;
            }

            appRouter.showItem(itemId, apiClient.serverId());
        });
    });
}

function setStatus(elem: HTMLElement, message: string, isError = false) {
    const status = elem.querySelector('.trendingNowHomeStatus');
    if (!status) {
        return;
    }

    status.textContent = message;
    status.classList.toggle('hide', !message);
    status.classList.toggle('is-error', Boolean(message) && isError);
}

function clearRotationTimer(elem: TrendingNowSectionElement) {
    if (elem._trendingRotationTimer) {
        window.clearInterval(elem._trendingRotationTimer);
        delete elem._trendingRotationTimer;
    }
}

function bindRotation(elem: TrendingNowSectionElement, state: RailState, apiClient: ApiClient) {
    clearRotationTimer(elem);

    let activeIndex = 0;
    const marketingLine = getRotatingMarketingLine();

    const renderActive = (index: number) => {
        activeIndex = wrapIndex(index, state.items.length);
        renderRail(
            elem,
            state,
            activeIndex,
            apiClient,
            marketingLine,
            () => renderActive(activeIndex - 1),
            () => renderActive(activeIndex + 1));
    };

    renderActive(0);

    if (state.items.length <= 1) {
        return;
    }

    elem._trendingRotationTimer = window.setInterval(() => {
        renderActive(activeIndex + 1);
    }, AUTO_ROTATE_MS);
}

export async function loadTrendingNow(
    elem: TrendingNowSectionElement,
    apiClient: ApiClient,
    user: UserDto,
    _options: SectionOptions
) {
    clearRotationTimer(elem);

    const userId = user.Id || apiClient.getCurrentUserId();

    elem.innerHTML = `
        <div class="sectionTitleContainer sectionTitleContainer-cards padded-left padded-right trendingNowHomeHeader">
            <div class="trendingNowHomeHeaderCopy">
                <h2 class="sectionTitle sectionTitle-cards">Trending Now</h2>
                <div class="trendingNowHomeSubtitle">Loading your next watch pick...</div>
            </div>
        </div>
        <div class="trendingNowHomeStatus padded-left padded-right">Loading trending titles...</div>
    `;

    try {
        const state = await loadRailState(apiClient, userId);
        if (!state.items.length) {
            elem.classList.add('hide');
            return;
        }

        elem.classList.remove('hide');
        setStatus(elem, '');
        bindRotation(elem, state, apiClient);
    } catch (error) {
        console.error('[TrendingNowHomeSection] failed to load data', error);
        setStatus(elem, 'Trending titles could not load right now.', true);
    }
}
