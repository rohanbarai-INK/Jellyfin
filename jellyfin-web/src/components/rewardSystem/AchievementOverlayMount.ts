import {
    ACHIEVEMENT_OVERLAY_ROOT_ID,
    REWARD_SYSTEM_HIDE_EVENT,
    REWARD_SYSTEM_SHOW_EVENT,
    RewardSystem,
    type ActiveReward,
    type RewardPayload
} from './RewardSystem';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import {
    syncAchievements as syncAchievementsApi,
    unlockAchievement as unlockAchievementApi,
    type AchievementUnlockResult,
    type UserAchievementRow
} from 'utils/achievementsApi';

import { addAchievementHistoryEntry } from './achievementHistoryStore';

const STYLE_ELEMENT_ID = 'reward-achievement-overlay-style';

const RARITY_COLORS: Record<string, string> = {
    common: '#a8b2c1',
    uncommon: '#69c779',
    rare: '#4fc3f7',
    epic: '#ce93d8',
    legendary: '#ffd700'
};

const RARITY_GLOW: Record<string, string> = {
    common: 'rgba(168,178,193,0.45)',
    uncommon: 'rgba(105,199,121,0.45)',
    rare: 'rgba(79,195,247,0.5)',
    epic: 'rgba(206,147,216,0.55)',
    legendary: 'rgba(255,215,0,0.65)'
};

const RARITY_DEFAULT_REWARDS: Record<string, { xp: number; coins: number }> = {
    common: { xp: 25, coins: 5 },
    uncommon: { xp: 75, coins: 15 },
    rare: { xp: 200, coins: 40 },
    legendary: { xp: 500, coins: 100 },
    epic: { xp: 200, coins: 40 }
};

const CONFETTI_COLORS = ['#ffd700', '#ff6b6b', '#4fc3f7', '#ce93d8', '#69f0ae', '#ffab40'];

const achievementOverlayStyle = `
@keyframes rewardAchievementSlideDown {
  0%   { transform: translateX(-50%) translateY(-140px); opacity: 0; }
  60%  { transform: translateX(-50%) translateY(10px); opacity: 1; }
  80%  { transform: translateX(-50%) translateY(-5px); opacity: 1; }
  100% { transform: translateX(-50%) translateY(0px); opacity: 1; }
}

@keyframes rewardAchievementSlideUp {
  0%   { transform: translateX(-50%) translateY(0px); opacity: 1; }
  100% { transform: translateX(-50%) translateY(-140px); opacity: 0; }
}

@keyframes rewardAchievementShimmer {
  0%   { background-position: -400px 0; }
  100% { background-position: 400px 0; }
}

@keyframes rewardAchievementGlow {
  0%,100% { box-shadow: 0 0 20px 4px rgba(255,200,50,0.4),0 8px 40px rgba(0,0,0,0.6); }
  50% { box-shadow: 0 0 40px 12px rgba(255,200,50,0.75),0 8px 40px rgba(0,0,0,0.6); }
}

@keyframes rewardAchievementWiggle {
  0%,100% { transform: rotate(0deg) scale(1); }
  20% { transform: rotate(-12deg) scale(1.15); }
  40% { transform: rotate(12deg) scale(1.15); }
  60% { transform: rotate(-7deg) scale(1.07); }
  80% { transform: rotate(7deg) scale(1.07); }
}

@keyframes rewardAchievementConfettiFall {
  0% { transform: translateY(-20px) rotate(0deg); opacity: 1; }
  100% { transform: translateY(90px) rotate(400deg); opacity: 0; }
}

@keyframes rewardAchievementProgressFill {
  from { width: 0%; }
  to { width: var(--ach-progress); }
}

.reward-achievement-toast {
  position: fixed;
  top: 24px;
  left: 50%;
  transform: translateX(-50%) translateY(-140px);
  z-index: 2;
  width: 420px;
  max-width: calc(100vw - 24px);
  border-radius: 18px;
  cursor: pointer;
  pointer-events: none;
  opacity: 0;
  filter: drop-shadow(0 0 18px rgba(255,215,0,0.6));
}

.reward-achievement-toast.ach-show {
  pointer-events: auto;
  animation:
    rewardAchievementSlideDown 0.65s cubic-bezier(0.34,1.56,0.64,1) forwards,
    rewardAchievementGlow 1.5s ease-in-out 0.7s infinite;
}

.reward-achievement-toast.ach-hide {
  animation: rewardAchievementSlideUp 0.45s ease-in forwards !important;
}

.reward-ach-inner {
  display: flex;
  align-items: center;
  background: linear-gradient(135deg,#1a1d2e 0%,#12142080 100%);
  backdrop-filter: blur(22px);
  border: 1.5px solid rgba(255,200,50,0.45);
  border-radius: 18px;
  overflow: hidden;
  min-height: 100px;
  position: relative;
}

.reward-ach-inner::before {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(105deg,transparent 30%,rgba(255,220,80,0.12) 50%,transparent 70%);
  background-size: 400px 100%;
  animation: rewardAchievementShimmer 2.5s linear infinite;
  border-radius: inherit;
  pointer-events: none;
}

.reward-ach-inner::after {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 5px;
  background: linear-gradient(180deg,#ffd700,#ff8c00);
  border-radius: 18px 0 0 18px;
}

.reward-ach-img-col {
  flex-shrink: 0;
  width: 96px;
  align-self: stretch;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 14px 6px 14px 18px;
}

.reward-ach-img-col img,
.reward-ach-img-col .reward-ach-emoji {
  width: 64px;
  height: 64px;
  object-fit: contain;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 52px;
  line-height: 1;
  animation: rewardAchievementWiggle 0.9s ease-in-out 0.7s;
  filter: drop-shadow(0 0 10px rgba(255,200,50,0.55));
}

.reward-ach-text-col {
  flex: 1;
  padding: 14px 40px 14px 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.reward-ach-label {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 2px;
  text-transform: uppercase;
  color: #ffd700;
}

.reward-ach-title {
  font-size: 17px;
  font-weight: 800;
  color: #ffffff;
  line-height: 1.25;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.reward-ach-desc {
  font-size: 12.5px;
  color: rgba(255,255,255,0.6);
  line-height: 1.45;
}

.reward-ach-meta-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 6px;
  flex-wrap: wrap;
}

.reward-ach-xp {
  display: inline-block;
  background: rgba(255,215,0,0.12);
  border: 1px solid rgba(255,215,0,0.3);
  color: #ffd700;
  font-size: 10px;
  font-weight: 700;
  padding: 2px 10px;
  border-radius: 99px;
  letter-spacing: 0.5px;
}

.reward-ach-coins {
  display: inline-block;
  background: rgba(255,196,64,0.12);
  border: 1px solid rgba(255,196,64,0.3);
  color: #ffd166;
  font-size: 10px;
  font-weight: 700;
  padding: 2px 10px;
  border-radius: 99px;
  letter-spacing: 0.5px;
}

.reward-ach-unlock-time {
  font-size: 10px;
  color: rgba(255,255,255,0.35);
  display: flex;
  align-items: center;
  gap: 3px;
}

.reward-ach-unlock-time::before {
  content: '\\1F550';
  font-size: 9px;
}

.reward-ach-progress-wrap {
  margin-top: 8px;
  background: rgba(255,255,255,0.1);
  border-radius: 99px;
  height: 5px;
  overflow: hidden;
}

.reward-ach-progress-bar {
  height: 100%;
  border-radius: 99px;
  background: linear-gradient(90deg,#ffd700,#ff8c00);
  animation: rewardAchievementProgressFill 1s ease 0.9s forwards;
  width: 0%;
}

.reward-ach-close {
  position: absolute;
  top: 9px;
  right: 11px;
  background: rgba(255,255,255,0.08);
  border: none;
  color: rgba(255,255,255,0.5);
  width: 22px;
  height: 22px;
  border-radius: 50%;
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s, color 0.2s;
  z-index: 10;
}

.reward-ach-close:hover {
  background: rgba(255,60,60,0.35);
  color: #fff;
}

.reward-ach-confetti {
  position: absolute;
  border-radius: 2px;
  animation: rewardAchievementConfettiFall 1.1s ease-out forwards;
  pointer-events: none;
}

@media (max-width: 480px) {
  .reward-achievement-toast {
    width: calc(100vw - 20px);
  }

  .reward-ach-title {
    font-size: 15px;
  }

  .reward-ach-img-col {
    width: 76px;
  }

  .reward-ach-img-col img,
  .reward-ach-img-col .reward-ach-emoji {
    width: 52px;
    height: 52px;
    font-size: 42px;
  }
}
`;

type AchievementShowEvent = Event & {
    detail: ActiveReward;
};

interface AchievementFields extends NonNullable<RewardPayload['achievement']> {
    rarity: 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary';
    progress: number;
    duration: number;
    xp: number;
    coins: number;
    imageEmoji?: string;
}

export interface ShowAchievementOptions {
    id?: string;
    title: string;
    description: string;
    imageEmoji?: string;
    emoji?: string;
    imageUrl?: string;
    rarity?: 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary';
    xp?: number;
    coins?: number;
    progress?: number;
    duration?: number;
    historyDisabled?: boolean;
    unlockedAt?: string;
    isSeasonal?: boolean;
    seasonType?: string;
    seasonYear?: number;
}

function ensureStyleElement() {
    if (document.getElementById(STYLE_ELEMENT_ID)) {
        return;
    }

    const style = document.createElement('style');
    style.id = STYLE_ELEMENT_ID;
    style.textContent = achievementOverlayStyle;
    document.head.appendChild(style);
}

function normalizeRarity(rarity?: string): AchievementFields['rarity'] {
    if (rarity === 'uncommon' || rarity === 'rare' || rarity === 'epic' || rarity === 'legendary') {
        return rarity;
    }

    return 'common';
}

function normalizeAchievementPayload(payload: RewardPayload, fallbackDuration = 5000): AchievementFields | null {
    if (!payload.achievement) {
        return null;
    }

    const title = payload.achievement.title?.trim();
    const description = payload.achievement.description?.trim();
    if (!title || !description) {
        return null;
    }

    const rarity = normalizeRarity(payload.achievement.rarity);
    const progressRaw = payload.achievement.progress;
    const progress = Number.isFinite(progressRaw) ? Math.max(0, Math.min(100, Number(progressRaw))) : 100;
    const defaultReward = RARITY_DEFAULT_REWARDS[rarity] || RARITY_DEFAULT_REWARDS.common;
    const xpFromPayload = payload.xpEarned;
    const xp = Number.isFinite(xpFromPayload) ? Number(xpFromPayload) : defaultReward.xp;
    const coinsFromPayload = payload.coinsEarned;
    const coins = Number.isFinite(coinsFromPayload) ? Number(coinsFromPayload) : defaultReward.coins;
    const durationRaw = payload.achievement.duration;
    const duration = Number.isFinite(durationRaw) ? Math.max(1200, Number(durationRaw)) : fallbackDuration;
    const imageEmoji = payload.achievement.imageEmoji || payload.achievement.emoji;

    return {
        ...payload.achievement,
        title,
        description,
        imageEmoji,
        emoji: imageEmoji,
        rarity,
        progress,
        duration,
        xp,
        coins
    };
}

function randomFloat() {
    if (window.crypto?.getRandomValues) {
        const values = new Uint32Array(1);
        window.crypto.getRandomValues(values);
        return values[0] / 4294967296;
    }

    // eslint-disable-next-line sonarjs/pseudo-random
    return Math.random();
}

export function mountAchievementOverlay() {
    if (typeof window === 'undefined' || typeof document === 'undefined') {
        return () => undefined;
    }

    const host = document.getElementById(ACHIEVEMENT_OVERLAY_ROOT_ID);
    if (!host) {
        return () => undefined;
    }

    ensureStyleElement();

    const root = document.createElement('div');
    root.className = 'reward-achievement-root';
    root.innerHTML = `
      <div class="reward-achievement-toast" role="alert" aria-live="assertive">
        <div class="reward-ach-inner">
          <button class="reward-ach-close" type="button" aria-label="Dismiss">&#x2715;</button>
          <div class="reward-ach-img-col"></div>
          <div class="reward-ach-text-col">
            <span class="reward-ach-label">Achievement Unlocked</span>
            <div class="reward-ach-title"></div>
            <div class="reward-ach-desc"></div>
            <div class="reward-ach-meta-row">
              <span class="reward-ach-xp">+0 XP</span>
              <span class="reward-ach-coins">+0 Coins</span>
              <span class="reward-ach-unlock-time"></span>
            </div>
            <div class="reward-ach-progress-wrap" style="display:none">
              <div class="reward-ach-progress-bar"></div>
            </div>
          </div>
        </div>
      </div>
    `;
    host.replaceChildren(root);

    const toast = root.querySelector('.reward-achievement-toast') as HTMLDivElement;
    const inner = root.querySelector('.reward-ach-inner') as HTMLDivElement;
    const imgCol = root.querySelector('.reward-ach-img-col') as HTMLDivElement;
    const labelEl = root.querySelector('.reward-ach-label') as HTMLSpanElement;
    const titleEl = root.querySelector('.reward-ach-title') as HTMLDivElement;
    const descEl = root.querySelector('.reward-ach-desc') as HTMLDivElement;
    const xpEl = root.querySelector('.reward-ach-xp') as HTMLSpanElement;
    const coinsEl = root.querySelector('.reward-ach-coins') as HTMLSpanElement;
    const unlockTimeEl = root.querySelector('.reward-ach-unlock-time') as HTMLSpanElement;
    const progressWrap = root.querySelector('.reward-ach-progress-wrap') as HTMLDivElement;
    const progressBar = root.querySelector('.reward-ach-progress-bar') as HTMLDivElement;
    const closeButton = root.querySelector('.reward-ach-close') as HTMLButtonElement;

    let activeRewardId: number | null = null;
    let autoDismissTimer: number | null = null;
    let animationCleanupTimer: number | null = null;
    let currentHistoryDisabled = false;

    const clearAutoDismissTimer = () => {
        if (autoDismissTimer != null) {
            window.clearTimeout(autoDismissTimer);
            autoDismissTimer = null;
        }
    };

    const clearAnimationCleanupTimer = () => {
        if (animationCleanupTimer != null) {
            window.clearTimeout(animationCleanupTimer);
            animationCleanupTimer = null;
        }
    };

    const clearConfetti = () => {
        root.querySelectorAll('.reward-ach-confetti').forEach(node => {
            node.remove();
        });
    };

    const hideToast = (completeReward: boolean) => {
        if (activeRewardId == null) {
            return;
        }

        const idToComplete = activeRewardId;
        clearAutoDismissTimer();
        clearAnimationCleanupTimer();
        toast.classList.remove('ach-show');
        toast.classList.add('ach-hide');

        animationCleanupTimer = window.setTimeout(() => {
            toast.classList.remove('ach-hide');
            clearConfetti();
            activeRewardId = null;

            if (completeReward) {
                RewardSystem.completeActiveReward(idToComplete);
            }
        }, 480);
    };

    const spawnConfetti = () => {
        clearConfetti();

        for (let i = 0; i < 14; i++) {
            const dot = document.createElement('span');
            const size = randomFloat() * 6 + 5;
            dot.className = 'reward-ach-confetti';
            dot.style.left = `${randomFloat() * 85 + 5}%`;
            dot.style.top = `${randomFloat() * 55}%`;
            dot.style.width = `${size}px`;
            dot.style.height = `${size}px`;
            dot.style.background = CONFETTI_COLORS[i % CONFETTI_COLORS.length];
            dot.style.borderRadius = size > 8 ? '50%' : '2px';
            dot.style.animationDelay = `${(randomFloat() * 0.5 + 0.6).toFixed(2)}s`;
            inner.appendChild(dot);
        }
    };

    const renderAchievementToast = (reward: ActiveReward) => {
        const achievement = normalizeAchievementPayload(reward.payload);
        if (!achievement) {
            RewardSystem.completeActiveReward(reward.id);
            return;
        }

        const color = RARITY_COLORS[achievement.rarity] || RARITY_COLORS.common;
        const glow = RARITY_GLOW[achievement.rarity] || RARITY_GLOW.common;
        const unlockedAtDate = achievement.unlockedAt ? new Date(achievement.unlockedAt) : new Date();
        const normalizedUnlockedAtDate = Number.isNaN(unlockedAtDate.getTime()) ? new Date() : unlockedAtDate;
        const unlockedAt = normalizedUnlockedAtDate.toISOString();
        const timeStr = normalizedUnlockedAtDate.toLocaleTimeString([], {
            hour: '2-digit',
            minute: '2-digit'
        });

        currentHistoryDisabled = achievement.historyDisabled === true;
        activeRewardId = reward.id;
        clearAutoDismissTimer();
        clearAnimationCleanupTimer();
        toast.classList.remove('ach-hide');
        toast.classList.remove('ach-show');

        titleEl.textContent = achievement.title;
        descEl.textContent = achievement.description;
        xpEl.textContent = `+${achievement.xp} XP`;
        coinsEl.textContent = `+${achievement.coins} Coins`;
        labelEl.textContent = `${achievement.rarity.charAt(0).toUpperCase()}${achievement.rarity.slice(1)} Achievement Unlocked`;
        unlockTimeEl.textContent = `Unlocked at ${timeStr}`;
        labelEl.style.color = color;
        xpEl.style.color = color;
        xpEl.style.borderColor = `${color}44`;
        xpEl.style.background = `${color}18`;
        coinsEl.style.borderColor = `${color}44`;
        inner.style.borderColor = `${color}66`;
        toast.style.filter = `drop-shadow(0 0 18px ${glow})`;

        imgCol.innerHTML = '';
        if (achievement.imageUrl) {
            const img = document.createElement('img');
            img.src = achievement.imageUrl;
            img.alt = achievement.title;
            imgCol.appendChild(img);
        } else {
            const badge = document.createElement('span');
            badge.className = 'reward-ach-emoji';
            badge.textContent = achievement.imageEmoji || achievement.emoji || '\uD83C\uDFC6';
            badge.style.filter = `drop-shadow(0 0 10px ${glow})`;
            imgCol.appendChild(badge);
        }

        if (achievement.progress < 100) {
            progressWrap.style.display = 'block';
            progressBar.style.setProperty('--ach-progress', `${achievement.progress}%`);
            progressBar.style.animation = 'none';
            progressBar.getBoundingClientRect();
            progressBar.style.animation = 'rewardAchievementProgressFill 1s ease 0.9s forwards';
        } else {
            progressWrap.style.display = 'none';
        }

        spawnConfetti();
        toast.classList.add('ach-show');
        if (!currentHistoryDisabled) {
            addAchievementHistoryEntry({
                achievementId: achievement.id,
                userId: ServerConnections.currentApiClient()?.getCurrentUserId(),
                title: achievement.title,
                description: achievement.description,
                emoji: achievement.imageEmoji || achievement.emoji,
                imageUrl: achievement.imageUrl,
                rarity: achievement.rarity,
                xp: achievement.xp,
                coins: achievement.coins,
                unlockedAt,
                isSeasonal: achievement.isSeasonal === true,
                seasonType: achievement.seasonType,
                seasonYear: Number.isFinite(achievement.seasonYear) ? Number(achievement.seasonYear) : undefined
            });
        }

        autoDismissTimer = window.setTimeout(() => {
            hideToast(true);
        }, achievement.duration);
    };

    const handleShowEvent = (event: Event) => {
        const reward = (event as AchievementShowEvent).detail;
        if (!reward || reward.type !== 'achievement') {
            return;
        }

        renderAchievementToast(reward);
    };

    const handleHideEvent = (event: Event) => {
        const reward = (event as AchievementShowEvent).detail;
        if (!reward || reward.type !== 'achievement') {
            return;
        }

        if (activeRewardId === reward.id) {
            hideToast(false);
        }
    };

    const dismissFromUser = (event?: Event) => {
        event?.stopPropagation();
        hideToast(true);
    };

    closeButton.addEventListener('click', dismissFromUser);
    toast.addEventListener('click', dismissFromUser);
    window.addEventListener(REWARD_SYSTEM_SHOW_EVENT, handleShowEvent);
    window.addEventListener(REWARD_SYSTEM_HIDE_EVENT, handleHideEvent);

    return () => {
        closeButton.removeEventListener('click', dismissFromUser);
        toast.removeEventListener('click', dismissFromUser);
        window.removeEventListener(REWARD_SYSTEM_SHOW_EVENT, handleShowEvent);
        window.removeEventListener(REWARD_SYSTEM_HIDE_EVENT, handleHideEvent);
        clearAutoDismissTimer();
        clearAnimationCleanupTimer();
        host.replaceChildren();
    };
}

export function showAchievement(options: ShowAchievementOptions) {
    RewardSystem.enqueue({
        xpEarned: options.xp,
        coinsEarned: options.coins,
        achievement: {
            id: options.id,
            title: options.title,
            description: options.description,
            imageEmoji: options.imageEmoji || options.emoji,
            emoji: options.imageEmoji || options.emoji,
            imageUrl: options.imageUrl,
            rarity: options.rarity,
            progress: options.progress,
            duration: options.duration,
            historyDisabled: options.historyDisabled,
            unlockedAt: options.unlockedAt,
            isSeasonal: options.isSeasonal,
            seasonType: options.seasonType,
            seasonYear: options.seasonYear
        }
    });
}

export async function unlockAchievementAndShow(achievementId: string): Promise<AchievementUnlockResult> {
    const normalizedAchievementId = achievementId.trim().toLowerCase();
    if (!normalizedAchievementId) {
        throw new Error('Achievement id is required.');
    }

    const result = await unlockAchievementApi(normalizedAchievementId);
    if (!result.unlocked || !result.achievement?.id) {
        return result;
    }

    RewardSystem.enqueue({
        xpEarned: result.achievement.xp,
        coinsEarned: result.achievement.coins,
        achievement: {
            id: result.achievement.id,
            title: result.achievement.title,
            description: result.achievement.description,
            imageEmoji: result.achievement.imageEmoji,
            emoji: result.achievement.imageEmoji,
            rarity: result.achievement.rarity,
            unlockedAt: result.achievement.unlockedAt,
            isSeasonal: result.achievement.isSeasonal,
            seasonType: result.achievement.seasonType,
            seasonYear: result.achievement.seasonYear ?? undefined
        }
    });

    return result;
}

export async function syncAchievementsAndShow(): Promise<UserAchievementRow[]> {
    const result = await syncAchievementsApi();
    const unlockedRows = [ ...result.unlockedAchievements ].sort((left, right) => {
        return new Date(left.unlockedAt).getTime() - new Date(right.unlockedAt).getTime();
    });

    unlockedRows.forEach((row) => {
        RewardSystem.enqueue({
            xpEarned: row.xp,
            coinsEarned: row.coins,
            achievement: {
                id: row.id,
                title: row.title,
                description: row.description,
                imageEmoji: row.imageEmoji,
                emoji: row.imageEmoji,
                rarity: row.rarity,
                unlockedAt: row.unlockedAt,
                isSeasonal: row.isSeasonal,
                seasonType: row.seasonType,
                seasonYear: row.seasonYear ?? undefined
            }
        });
    });

    return unlockedRows;
}
