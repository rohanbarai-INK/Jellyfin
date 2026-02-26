import React, { type ReactNode } from 'react';

import RequestEmptyState from './RequestEmptyState';

export interface RequestTableColumn<T> {
    key: string
    label: string
    className?: string
    render: (row: T) => ReactNode
}

interface RequestTableProps<T> {
    columns: RequestTableColumn<T>[]
    rows: T[]
    rowKey: (row: T) => string
    emptyMessage: string
    emptyTitle?: string
    emptySubtitle?: string
    emptyActionText?: string
    emptyActionHref?: string
}

const RequestTable = <T,>({
    columns,
    rows,
    rowKey,
    emptyMessage,
    emptyTitle,
    emptySubtitle,
    emptyActionText,
    emptyActionHref
}: RequestTableProps<T>) => (
    <div className='requestTableViewport requestDesktopOnly'>
        <table className='requestTable'>
            <thead>
                <tr>
                    {columns.map(column => (
                        <th key={column.key} className={column.className}>
                            {column.label}
                        </th>
                    ))}
                </tr>
            </thead>
            <tbody>
                {rows.map(row => {
                    const id = rowKey(row);

                    return (
                        <tr key={id}>
                            {columns.map(column => (
                                <td key={`${id}-${column.key}`} className={column.className}>
                                    {column.render(row)}
                                </td>
                            ))}
                        </tr>
                    );
                })}
                {rows.length === 0 && (
                    <tr>
                        <td colSpan={columns.length}>
                            <RequestEmptyState
                                message={emptyMessage}
                                title={emptyTitle}
                                subtitle={emptySubtitle}
                                actionText={emptyActionText}
                                actionHref={emptyActionHref}
                            />
                        </td>
                    </tr>
                )}
            </tbody>
        </table>
    </div>
);

export default RequestTable;
