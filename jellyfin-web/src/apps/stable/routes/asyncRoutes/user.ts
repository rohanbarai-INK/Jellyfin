import { AsyncRoute } from '../../../../components/router/AsyncRoute';

export const ASYNC_USER_ROUTES: AsyncRoute[] = [
    { path: 'mypreferencesmenu', page: 'user/settings' },
    { path: 'personalinsights', page: 'user/personalinsights' },
    { path: 'quickconnect', page: 'quickConnect' },
    { path: 'request', page: 'request' },
    { path: 'search', page: 'search' },
    { path: 'subscription', page: 'subscription' },
    { path: 'userprofile', page: 'user/userprofile' }
];
