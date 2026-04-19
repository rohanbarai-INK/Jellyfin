import React, { type FC, useMemo } from 'react';

import globalize from 'lib/globalize';

interface RequestPaginationProps {
    pageIndex: number
    pageSize: number
    totalRecordCount: number
    isBusy?: boolean
    onPageChange: (nextPageIndex: number) => void
}

const RequestPagination: FC<RequestPaginationProps> = ({
    pageIndex,
    pageSize,
    totalRecordCount,
    isBusy = false,
    onPageChange
}) => {
    const totalPages = useMemo(() => {
        const safePageSize = Math.max(1, pageSize);
        return Math.max(1, Math.ceil(Math.max(0, totalRecordCount) / safePageSize));
    }, [ pageSize, totalRecordCount ]);

    const canGoPrevious = pageIndex > 0 && !isBusy;
    const canGoNext = pageIndex < totalPages - 1 && !isBusy;

    return (
        <div className='requestPagination'>
            <button
                className='requestActionButton'
                type='button'
                onClick={() => onPageChange(pageIndex - 1)}
                disabled={!canGoPrevious}
            >
                {globalize.translate('Previous')}
            </button>
            <span className='requestPaginationLabel'>
                {globalize.translate('RequestPaginationPageLabel', (pageIndex + 1).toString(), totalPages.toString())}
            </span>
            <button
                className='requestActionButton'
                type='button'
                onClick={() => onPageChange(pageIndex + 1)}
                disabled={!canGoNext}
            >
                {globalize.translate('Next')}
            </button>
        </div>
    );
};

export default RequestPagination;
