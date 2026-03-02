import { AsyncRoute } from 'components/router/AsyncRoute';
import { AppType } from 'constants/appType';

export const ASYNC_USER_ROUTES: AsyncRoute[] = [
    { path: 'achievements', page: 'user/achievements' },
    { path: 'home', type: AppType.Experimental },
    { path: 'homevideos', type: AppType.Experimental },
    { path: 'livetv', type: AppType.Experimental },
    { path: 'movies', type: AppType.Experimental },
    { path: 'music', type: AppType.Experimental },
    { path: 'mypreferencesdisplay', page: 'user/display', type: AppType.Experimental },
    { path: 'mypreferencesmenu', page: 'user/settings' },
    { path: 'personalinsights', page: 'user/personalinsights' },
    { path: 'quickconnect', page: 'quickConnect' },
    { path: 'request', page: 'request', type: AppType.Stable },
    { path: 'search' },
    { path: 'subscription', page: 'subscription', type: AppType.Stable },
    { path: 'tv', page: 'shows', type: AppType.Experimental },
    { path: 'userprofile', page: 'user/userprofile' }
];
