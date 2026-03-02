import { useCallback, useState } from 'react';

import { type NotificationItem } from '../types';

function createNotificationId(): string {
    if (typeof window !== 'undefined' && window.crypto?.getRandomValues) {
        const values = new Uint32Array(2);
        window.crypto.getRandomValues(values);
        return `${Date.now().toString(36)}-${values[0].toString(36)}${values[1].toString(36)}`;
    }

    // eslint-disable-next-line sonarjs/pseudo-random
    return `${Date.now().toString(36)}-${Math.floor(Math.random() * 1000000000).toString(36)}`;
}

export const useProgressiveSystem = () => {
    const [ score, setScore ] = useState(0);
    const [ notifications, setNotifications ] = useState<NotificationItem[]>([]);

    const addScore = useCallback((amount: number) => {
        const normalizedAmount = Number(amount);
        if (!Number.isFinite(normalizedAmount) || normalizedAmount <= 0) {
            return;
        }

        setScore(prev => prev + normalizedAmount);

        const newNotification: NotificationItem = {
            id: createNotificationId(),
            value: normalizedAmount
        };

        setNotifications(prev => [...prev, newNotification]);
    }, []);

    const handleNotificationComplete = useCallback((id: string) => {
        setNotifications(prev => prev.filter(item => item.id !== id));
    }, []);

    return {
        score,
        notifications,
        addScore,
        handleNotificationComplete
    };
};
