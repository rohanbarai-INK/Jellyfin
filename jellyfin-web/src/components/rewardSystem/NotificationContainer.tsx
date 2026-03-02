import React from 'react';

import { type NotificationItem } from 'types';

import FloatingItem from './FloatingItem';

interface NotificationContainerProps {
    items: NotificationItem[];
    onComplete: (id: string) => void;
}

const NotificationContainer = ({
    items,
    onComplete
}: NotificationContainerProps) => {
    if (items.length === 0) {
        return null;
    }

    return (
        <div className='coinRewardNotificationContainer' aria-live='polite'>
            {items.map((item) => (
                <FloatingItem
                    key={item.id}
                    id={item.id}
                    value={item.value}
                    onComplete={onComplete}
                />
            ))}
        </div>
    );
};

export default NotificationContainer;
