const { merge } = require('webpack-merge');

const common = require('./webpack.common');

const JELLYFIN_DEV_SERVER_TARGET = process.env.JELLYFIN_SERVER || 'http://127.0.0.1:8096';
const STATIC_FILE_REGEX = /\.[a-z0-9]+($|\?)/i;
const DEV_SERVER_ONLY_PREFIXES = [
    '/sockjs-node',
    '/ws',
    '/webpack-dev-server',
    '/__webpack_dev_server__'
];
const DEV_SERVER_ONLY_PATHS = new Set([
    '/',
    '/index.html',
    '/config.json',
    '/manifest.json',
    '/robots.txt'
]);

/**
 * Proxy Jellyfin API/websocket traffic to a running Jellyfin server
 * while keeping webpack static assets served from the dev server.
 */
function shouldProxyToServer(pathname) {
    if (DEV_SERVER_ONLY_PATHS.has(pathname)) {
        return false;
    }

    if (DEV_SERVER_ONLY_PREFIXES.some(prefix => pathname.startsWith(prefix))) {
        return false;
    }

    return !STATIC_FILE_REGEX.test(pathname);
}

module.exports = merge(common, {
    // In order for live reload to work we must use "web" as the target not "browserslist"
    target: process.env.WEBPACK_SERVE ? 'web' : 'browserslist',
    mode: 'development',
    devtool: 'eval-cheap-module-source-map',
    module: {
        rules: [
            {
                test: /\.(js|jsx|ts|tsx)$/,
                exclude: /node_modules/,
                enforce: 'pre',
                use: ['source-map-loader']
            }
        ]
    },
    devServer: {
        compress: true,
        client: {
            overlay: {
                errors: true,
                warnings: false
            }
        },
        proxy: [
            {
                context: pathname => shouldProxyToServer(pathname),
                target: JELLYFIN_DEV_SERVER_TARGET,
                changeOrigin: true,
                secure: false,
                ws: true
            }
        ]
    }
});
