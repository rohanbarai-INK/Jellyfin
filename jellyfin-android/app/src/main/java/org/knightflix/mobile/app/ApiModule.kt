package org.knightflix.mobile.app

import org.knightflix.mobile.utils.Constants
import org.jellyfin.sdk.Jellyfin
import org.jellyfin.sdk.api.okhttp.OkHttpFactory
import org.jellyfin.sdk.createJellyfin
import org.jellyfin.sdk.model.ClientInfo
import org.knightflix.mobile.subscription.SubscriptionExpiryInterceptor
import okhttp3.OkHttpClient
import org.koin.android.ext.koin.androidContext
import org.koin.dsl.module

val apiModule = module {
    single { SubscriptionExpiryInterceptor(get()) }
    single {
        OkHttpFactory(
            OkHttpClient.Builder()
                .addInterceptor(get<SubscriptionExpiryInterceptor>())
                .build(),
        )
    }

    // Jellyfin API builder and API client instance
    single {
        createJellyfin {
            context = androidContext()
            clientInfo = ClientInfo(name = Constants.APP_INFO_NAME, version = Constants.APP_INFO_VERSION)
            apiClientFactory = get<OkHttpFactory>()
            socketConnectionFactory = get<OkHttpFactory>()
        }
    }
    single { get<Jellyfin>().createApi() }
}
