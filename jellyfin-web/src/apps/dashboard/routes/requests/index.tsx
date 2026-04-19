import React, { useCallback, useEffect, useState } from 'react';

import AdminCompleteModal from 'components/contentRequests/AdminCompleteModal';
import AdminRewardQuotaManager from 'components/contentRequests/AdminRewardQuotaManager';
import AdminRequestTable from 'components/contentRequests/AdminRequestTable';
import RequestHeader from 'components/contentRequests/RequestHeader';
import RequestPageContainer from 'components/contentRequests/RequestPageContainer';
import 'components/contentRequests/contentRequests.scss';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import { useApi } from 'hooks/useApi';
import globalize from 'lib/globalize';
import { queryClient } from 'utils/query/queryClient';
import {
    type ContentRequestRow,
    approveContentRequest,
    completeContentRequest,
    getAdminContentRequestsPaged,
    rejectContentRequest
} from 'utils/contentRequestsApi';

import { CONTENT_REQUEST_QUERY_KEYS } from 'apps/dashboard/features/contentRequests/api/queryKeys';
import './requests.scss';
const ADMIN_PAGE_SIZE = 10;

const getErrorMessage = async (error: unknown) => {
    if (!error || typeof error !== 'object') {
        return globalize.translate('UnknownError');
    }

    const responseError = error as {
        text?: () => Promise<string>
        response?: {
            data?: unknown
        }
    };

    const responseData = responseError.response?.data;
    if (typeof responseData === 'string' && responseData.trim()) {
        return responseData;
    }

    if (responseData && typeof responseData === 'object') {
        const payload = responseData as Record<string, unknown>;
        const message = payload.message ?? payload.Message ?? payload.error ?? payload.Error;
        if (typeof message === 'string' && message.trim()) {
            return message;
        }
    }

    if (typeof responseError.text === 'function') {
        const text = await responseError.text();
        if (text.trim()) {
            return text;
        }
    }

    return globalize.translate('UnknownError');
};

export const Component = () => {
    const { __legacyApiClient__: apiClient } = useApi();
    const [ rows, setRows ] = useState<ContentRequestRow[]>([]);
    const [ totalRecordCount, setTotalRecordCount ] = useState(0);
    const [ pageIndex, setPageIndex ] = useState(0);
    const [ isLoading, setIsLoading ] = useState(true);
    const [ isTableLoading, setIsTableLoading ] = useState(false);

    const [ isBusy, setIsBusy ] = useState(false);
    const [ pageMessage, setPageMessage ] = useState('');
    const [ pageError, setPageError ] = useState(false);
    const [ completeTarget, setCompleteTarget ] = useState<ContentRequestRow | null>(null);

    const refreshData = useCallback(async (targetPageIndex = pageIndex) => {
        if (!apiClient) {
            return;
        }

        if (isLoading) {
            setIsLoading(true);
        } else {
            setIsTableLoading(true);
        }
        try {
            const response = await getAdminContentRequestsPaged(
                targetPageIndex * ADMIN_PAGE_SIZE,
                ADMIN_PAGE_SIZE,
                apiClient
            );

            setRows(response.items);
            setTotalRecordCount(response.totalRecordCount);
        } finally {
            setIsLoading(false);
            setIsTableLoading(false);
        }

        await queryClient.invalidateQueries({
            queryKey: [ CONTENT_REQUEST_QUERY_KEYS.adminUnseenPendingCount ]
        });
    }, [ apiClient, pageIndex ]);

    useEffect(() => {
        void refreshData(pageIndex);
    }, [ pageIndex, refreshData ]);

    const runAction = useCallback(async (action: () => Promise<unknown>) => {
        setIsBusy(true);
        setPageError(false);
        setPageMessage('');

        try {
            await action();
            await refreshData();
        } catch (error) {
            setPageError(true);
            setPageMessage(await getErrorMessage(error));
        } finally {
            setIsBusy(false);
        }
    }, [ refreshData ]);

    const onApprove = useCallback((requestId: string) => {
        void runAction(async () => {
            await approveContentRequest(requestId, apiClient || undefined);
        });
    }, [ apiClient, runAction ]);

    const onReject = useCallback((requestId: string) => {
        void runAction(async () => {
            await rejectContentRequest(requestId, apiClient || undefined);
        });
    }, [ apiClient, runAction ]);

    const onOpenCompleteFlow = useCallback((row: ContentRequestRow) => {
        setCompleteTarget(row);
        setPageError(false);
        setPageMessage('');
    }, []);

    const onCloseCompleteFlow = useCallback(() => {
        setCompleteTarget(null);
    }, []);

    const onConfirmComplete = useCallback(async (requestId: string, jellyfinItemId: string) => {
        if (!apiClient) {
            throw new Error(globalize.translate('UnknownError'));
        }

        setIsBusy(true);
        setPageError(false);
        setPageMessage('');

        try {
            await completeContentRequest(requestId, jellyfinItemId, apiClient);
            await refreshData();
            setCompleteTarget(null);
        } catch (error) {
            const message = await getErrorMessage(error);
            setPageError(true);
            setPageMessage(message);
            throw error;
        } finally {
            setIsBusy(false);
        }
    }, [ apiClient, refreshData ]);

    if (isLoading) {
        return <Loading />;
    }

    return (
        <Page
            id='dashboardRequestsPage'
            className='mainAnimatedPage type-interior requestAdminPageRoot'
            title={globalize.translate('RequestAdminTitle')}
            shouldAutoFocus
        >
            <RequestPageContainer>
                <section className='requestSection'>
                    <RequestHeader
                        title={globalize.translate('RequestAdminRewardTitle')}
                        subtitle={globalize.translate('RequestAdminRewardSubtitle')}
                    />
                    <AdminRewardQuotaManager
                        apiClient={apiClient || undefined}
                        isBusy={isBusy}
                        onRewardApplied={refreshData}
                    />
                </section>
                <section className='requestSection'>
                    <RequestHeader title={globalize.translate('RequestAdminTitle')} />
                    {!!pageMessage && (
                        <p className={`requestFormMessage${pageError ? ' error' : ''}`}>
                            {pageMessage}
                        </p>
                    )}
                    <AdminRequestTable
                        rows={rows}
                        pageIndex={pageIndex}
                        pageSize={ADMIN_PAGE_SIZE}
                        totalRecordCount={totalRecordCount}
                        isBusy={isBusy || isTableLoading}
                        onApprove={onApprove}
                        onReject={onReject}
                        onComplete={onOpenCompleteFlow}
                        onPageChange={setPageIndex}
                    />
                </section>
            </RequestPageContainer>

            <AdminCompleteModal
                open={!!completeTarget}
                target={completeTarget}
                apiClient={apiClient || undefined}
                isBusy={isBusy}
                onClose={onCloseCompleteFlow}
                onConfirm={onConfirmComplete}
            />
        </Page>
    );
};

Component.displayName = 'DashboardRequestsPage';
