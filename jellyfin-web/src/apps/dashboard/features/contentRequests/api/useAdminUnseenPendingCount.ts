import { useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';
import { getAdminUnseenPendingCount } from 'utils/contentRequestsApi';

import { CONTENT_REQUEST_QUERY_KEYS } from './queryKeys';

export const useAdminUnseenPendingCount = () => {
    const { __legacyApiClient__: apiClient } = useApi();

    return useQuery({
        queryKey: [ CONTENT_REQUEST_QUERY_KEYS.adminUnseenPendingCount, apiClient?.serverId() ],
        queryFn: () => getAdminUnseenPendingCount(apiClient),
        enabled: !!apiClient
    });
};
