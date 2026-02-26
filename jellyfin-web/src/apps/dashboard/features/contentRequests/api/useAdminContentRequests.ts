import { useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';
import { getAdminContentRequests } from 'utils/contentRequestsApi';

import { CONTENT_REQUEST_QUERY_KEYS } from './queryKeys';

export const useAdminContentRequests = () => {
    const { __legacyApiClient__: apiClient } = useApi();

    return useQuery({
        queryKey: [ CONTENT_REQUEST_QUERY_KEYS.adminRows, apiClient?.serverId() ],
        queryFn: () => getAdminContentRequests(apiClient),
        enabled: !!apiClient
    });
};
