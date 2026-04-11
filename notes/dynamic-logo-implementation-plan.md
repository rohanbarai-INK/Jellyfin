# Dynamic Logo Management Implementation Plan

## Overview

This plan outlines the steps required to implement dynamic logo management in the Jellyfin dashboard. The feature will allow admins to upload, preview, and delete custom logos, with changes reflected across all Jellyfin platforms.

## Project Scope

### Goals
1. Add logo configuration option to the branding settings
2. Implement server-side API endpoints for logo management
3. Create UI for logo upload, preview, and deletion
4. Update all clients to respect custom logo configuration
5. Ensure backward compatibility

### Out of Scope
1. Logo customization beyond upload/replacement (e.g., color, effects)
2. Dynamic logo per user/group
3. Logo animation customization

## Implementation Phases

### Phase 1: Server-Side Development

#### 1.1 Update Branding Configuration
**File:** `jellyfin/MediaBrowser.Model/Branding/BrandingOptions.cs`

```csharp
namespace MediaBrowser.Model.Branding;

public class BrandingOptions
{
    // Existing properties...
    public string? LoginDisclaimer { get; set; }
    public string? CustomCss { get; set; }
    public bool SplashscreenEnabled { get; set; } = false;
    public string? SplashscreenLocation { get; set; }
    
    // New properties for logo
    public string? LogoLocation { get; set; }
    public bool LogoEnabled { get; set; } = true;
    public string? LogoSize { get; set; } = "medium"; // small, medium, large
    public string? LogoPosition { get; set; } = "left"; // left, center, right
}
```

#### 1.2 Add Logo API Endpoints
**File:** `jellyfin/Jellyfin.Api/Controllers/ImageController.cs`

```csharp
// Upload custom logo
[HttpPost("Branding/Logo")]
[Authorize(Policy = Policies.RequiresElevation)]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[AcceptsImageFile]
public async Task<ActionResult> UploadCustomLogo()
{
    if (!TryGetImageExtensionFromContentType(Request.ContentType, out var extension))
    {
        return BadRequest("Incorrect ContentType.");
    }

    var stream = GetFromBase64Stream(Request.Body);
    await using (stream.ConfigureAwait(false))
    {
        var filePath = Path.Combine(_appPaths.DataPath, "logo-upload" + extension);
        var brandingOptions = _serverConfigurationManager.GetConfiguration<BrandingOptions>("branding");
        brandingOptions.LogoLocation = filePath;
        brandingOptions.LogoEnabled = true;
        _serverConfigurationManager.SaveConfiguration("branding", brandingOptions);

        var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, IODefaults.FileStreamBufferSize, FileOptions.Asynchronous);
        await using (fs.ConfigureAwait(false))
        {
            await stream.CopyToAsync(fs, CancellationToken.None).ConfigureAwait(false);
        }

        return NoContent();
    }
}

// Delete custom logo
[HttpDelete("Branding/Logo")]
[Authorize(Policy = Policies.RequiresElevation)]
[ProducesResponseType(StatusCodes.Status204NoContent)]
public ActionResult DeleteCustomLogo()
{
    var brandingOptions = _serverConfigurationManager.GetConfiguration<BrandingOptions>("branding");
    if (!string.IsNullOrEmpty(brandingOptions.LogoLocation)
        && System.IO.File.Exists(brandingOptions.LogoLocation))
    {
        System.IO.File.Delete(brandingOptions.LogoLocation);
        brandingOptions.LogoLocation = null;
        _serverConfigurationManager.SaveConfiguration("branding", brandingOptions);
    }

    return NoContent();
}

// Get custom logo
[HttpGet("Branding/Logo")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public ActionResult GetCustomLogo()
{
    var brandingOptions = _serverConfigurationManager.GetConfiguration<BrandingOptions>("branding");
    if (string.IsNullOrEmpty(brandingOptions.LogoLocation) || !System.IO.File.Exists(brandingOptions.LogoLocation))
    {
        return NotFound();
    }

    return PhysicalFile(brandingOptions.LogoLocation, "image/png");
}
```

#### 1.3 Update Branding API Response
**File:** `jellyfin/Jellyfin.Api/Controllers/BrandingController.cs`

```csharp
public class BrandingOptionsDto
{
    public string? LoginDisclaimer { get; set; }
    public string? CustomCss { get; set; }
    public bool SplashscreenEnabled { get; set; }
    public string? LogoLocation { get; set; }
    public bool LogoEnabled { get; set; }
    public string? LogoSize { get; set; }
    public string? LogoPosition { get; set; }
}

[HttpGet("Configuration")]
[ProducesResponseType(StatusCodes.Status200OK)]
public ActionResult<BrandingOptionsDto> GetBrandingOptions()
{
    var brandingOptions = _serverConfigurationManager.GetConfiguration<BrandingOptions>("branding");

    var brandingOptionsDto = new BrandingOptionsDto
    {
        LoginDisclaimer = brandingOptions.LoginDisclaimer,
        CustomCss = brandingOptions.CustomCss,
        SplashscreenEnabled = brandingOptions.SplashscreenEnabled,
        LogoEnabled = brandingOptions.LogoEnabled,
        LogoSize = brandingOptions.LogoSize,
        LogoPosition = brandingOptions.LogoPosition
    };

    return brandingOptionsDto;
}
```

### Phase 2: Web Client Development

#### 2.1 Update Branding Constants
**File:** `jellyfin-web/src/constants/branding.ts`

```typescript
export const SPLASHSCREEN_URL = '/Branding/Splashscreen';
export const LOGO_URL = '/Branding/Logo';
```

#### 2.2 Add Logo API Hook
**File:** `jellyfin-web/src/apps/dashboard/features/branding/api/useBrandingOptions.ts`

```typescript
import { Api } from '@jellyfin/sdk';
import { getBrandingApi } from '@jellyfin/sdk/lib/utils/api/branding-api';
import { getImageApi } from '@jellyfin/sdk/lib/utils/api/image-api';
import { queryOptions, useQuery } from '@tanstack/react-query';
import type { AxiosRequestConfig } from 'axios';

import { useApi } from 'hooks/useApi';
import { LOGO_URL } from 'constants/branding';

export const QUERY_KEY = 'BrandingOptions';

// Existing fetchBrandingOptions function...

export const uploadCustomLogo = async (api: Api, file: File) => {
    return new Promise<void>((resolve, reject) => {
        const reader = new FileReader();
        reader.onerror = reject;
        reader.onabort = reject;
        reader.onload = () => {
            const dataUrl = reader.result as string;
            const body = dataUrl.split(',')[1];
            getImageApi(api)
                .uploadCustomLogo({
                    body: body as never
                }, {
                    headers: { ['Content-Type']: file.type }
                })
                .then(resolve)
                .catch(reject);
        };
        reader.readAsDataURL(file);
    });
};

export const deleteCustomLogo = async (api: Api) => {
    return getImageApi(api).deleteCustomLogo();
};

export const getCustomLogoUrl = (api: Api) => {
    return api.getUri(LOGO_URL, { t: Date.now() });
};
```

#### 2.3 Update Branding Page UI
**File:** `jellyfin-web/src/apps/dashboard/routes/branding/index.tsx`

Add logo management section:

```typescript
// Import necessary components and hooks
import Upload from '@mui/icons-material/Upload';
import Delete from '@mui/icons-material/Delete';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import Button from '@mui/material/Button';
import Image from 'components/Image';

// Add logo state management
const [logoUrl, setLogoUrl] = useState<string>();
const [isLogoEnabled, setIsLogoEnabled] = useState(brandingOptions.LogoEnabled ?? true);
const [logoSize, setLogoSize] = useState(brandingOptions.LogoSize ?? 'medium');
const [logoPosition, setLogoPosition] = useState(brandingOptions.LogoPosition ?? 'left');

// Logo upload handler
const onLogoUpload = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
    const files = event.target.files;
    if (!api || !files) return;

    const file = files[0];
    setError(undefined);

    uploadCustomLogo(api, file)
        .then(() => {
            setLogoUrl(getCustomLogoUrl(api));
        })
        .catch(e => {
            console.error('[BrandingPage] error uploading logo', e);
            setError('ImageUploadFailed');
        });
}, [api]);

// Logo delete handler
const onLogoDelete = useCallback(() => {
    setError(undefined);
    if (!api) return;

    deleteCustomLogo(api)
        .then(() => {
            setLogoUrl(undefined);
        })
        .catch(e => {
            console.error('[BrandingPage] error deleting logo', e);
            setError('ImageDeleteFailed');
        });
}, [api]);

// Logo enabled handler
const setLogoEnabled = useCallback(async (_: React.ChangeEvent<HTMLInputElement>, isEnabled: boolean) => {
    setIsLogoEnabled(isEnabled);

    await getConfigurationApi(api!)
        .updateNamedConfiguration({
            key: BRANDING_CONFIG_KEY,
            body: JSON.stringify({
                ...defaultBrandingOptions,
                LogoEnabled: isEnabled
            })
        });

    void queryClient.invalidateQueries({
        queryKey: [ QUERY_KEY ]
    });
}, [ api, defaultBrandingOptions ]);

// Add logo section to UI
<Stack
    direction={{
        xs: 'column',
        sm: 'row'
    }}
    spacing={3}
>
    <Box sx={{ flex: '1 1 0' }}>
        {logoUrl && (
            <Image
                isLoading={false}
                url={logoUrl}
                alt={globalize.translate('CustomLogo')}
            />
        )}
    </Box>

    <Stack
        spacing={{ xs: 3, sm: 2 }}
        sx={{ flex: '1 1 0' }}
    >
        <FormControlLabel
            control={
                <Switch
                    checked={isLogoEnabled}
                    onChange={setLogoEnabled}
                />
            }
            label={globalize.translate('EnableCustomLogo')}
        />

        <Typography variant='body2'>
            {globalize.translate('CustomLogoSize')}
        </Typography>

        <Button
            component='label'
            variant='outlined'
            startIcon={<Upload />}
            disabled={!isLogoEnabled}
        >
            <input
                type='file'
                accept='image/*'
                hidden
                onChange={onLogoUpload}
            />
            {globalize.translate('UploadCustomImage')}
        </Button>

        <Button
            variant='outlined'
            color='error'
            startIcon={<Delete />}
            disabled={!isLogoEnabled || !logoUrl}
            onClick={onLogoDelete}
        >
            {globalize.translate('DeleteCustomImage')}
        </Button>

        <TextField
            select
            label={globalize.translate('LogoSize')}
            value={logoSize}
            onChange={(e) => setLogoSize(e.target.value)}
            disabled={!isLogoEnabled}
        >
            <MenuItem value='small'>{globalize.translate('Small')}</MenuItem>
            <MenuItem value='medium'>{globalize.translate('Medium')}</MenuItem>
            <MenuItem value='large'>{globalize.translate('Large')}</MenuItem>
        </TextField>

        <TextField
            select
            label={globalize.translate('LogoPosition')}
            value={logoPosition}
            onChange={(e) => setLogoPosition(e.target.value)}
            disabled={!isLogoEnabled}
        >
            <MenuItem value='left'>{globalize.translate('Left')}</MenuItem>
            <MenuItem value='center'>{globalize.translate('Center')}</MenuItem>
            <MenuItem value='right'>{globalize.translate('Right')}</MenuItem>
        </TextField>
    </Stack>
</Stack>
```

#### 2.4 Update Logo Screensaver
**File:** `jellyfin-web/src/plugins/logoScreensaver/plugin.js`

```javascript
import { LOGO_URL } from 'constants/branding';
import { ServerConnections } from 'lib/jellyfin-apiclient';

// Modify show method to use custom logo if available
self.show = function () {
    import('./style.scss').then(() => {
        let elem = document.querySelector('.logoScreenSaver');

        if (!elem) {
            elem = document.createElement('div');
            elem.classList.add('logoScreenSaver');
            document.body.appendChild(elem);

            // Check if custom logo is configured
            const api = ServerConnections.getCurrentApi();
            if (api) {
                const logoUrl = api.getUri(LOGO_URL);
                elem.innerHTML = `<img class="logoScreenSaverImage" src="${logoUrl}" onerror="this.src='${icon}'" />`;
            } else {
                elem.innerHTML = `<img class="logoScreenSaverImage" src="${icon}" />`;
            }
        }

        stopInterval();
        interval = setInterval(animate, 3000);
    });
};
```

### Phase 3: Client Applications

#### 3.1 Android App
**File:** `jellyfin-android/app/src/main/java/org/jellyfin/mobile/ui/screens/connect/ConnectScreen.kt`

Update to check for custom logo:

```kotlin
@Composable
fun LogoHeader() {
    CenterRow(
        modifier = Modifier.padding(vertical = 25.dp),
    ) {
        // Check if custom logo is available from server
        val logoUrl = remember { getCustomLogoUrl() }
        if (logoUrl != null) {
            Image(
                painter = rememberImagePainter(logoUrl),
                modifier = Modifier
                    .height(72.dp),
                contentDescription = null,
            )
        } else {
            // Fallback to default logo
            Image(
                painter = painterResource(R.drawable.app_logo),
                modifier = Modifier
                    .height(72.dp),
                contentDescription = null,
            )
        }
    }
}
```

#### 3.2 Android TV App
**Files:**
- `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/shared/toolbar/Toolbar.kt`
- `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/startup/fragment/SplashFragment.kt`
- `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/integration/dream/composable/DreamContentLogo.kt`

Update all places to check for custom logo from server.

### Phase 4: Testing and Validation

1. **End-to-end testing:** Verify the entire flow from upload to display
2. **Backward compatibility testing:** Ensure existing installations work correctly
3. **Cross-platform testing:** Check all platforms (web, Android, Android TV, Desktop)
4. **Performance testing:** Test with various logo sizes and formats
5. **Accessibility testing:** Ensure the UI is accessible to all users

## Estimated Timeline

| Phase | Estimated Time |
|-------|----------------|
| Server-side Development | 1 week |
| Web Client Development | 1 week |
| Android App Updates | 3 days |
| Android TV App Updates | 3 days |
| Testing and Validation | 3 days |
| Documentation and Release | 2 days |
| **Total** | **3 weeks** |

## Risk Assessment

### High Priority Risks
1. **File format compatibility issues** - Need to support common image formats
2. **Performance impacts** - Large logo files could slow down the interface
3. **Memory leaks** - File upload/download could cause memory issues if not properly managed

### Medium Priority Risks
1. **Responsive design issues** - Logo may not scale properly on different screen sizes
2. **Cache invalidation** - Changes may not reflect immediately due to caching
3. **Localization** - UI strings need to be properly translated

### Low Priority Risks
1. **Browser compatibility** - Different browsers may handle image loading differently
2. **Edge cases** - Handling of corrupted files, network errors, etc.

## Success Criteria

1. Admin can upload a custom logo from the branding page
2. Custom logo is displayed correctly on all platforms
3. Admin can preview and delete custom logo
4. Default logo is used as fallback if custom logo not configured
5. All existing features continue to work
6. Changes are reflected without requiring a server restart

## Future Enhancements

1. **Logo customization options:** Allow resizing, cropping, and effects
2. **Logo templates:** Provide pre-designed logo templates
3. **Multi-tenant support:** Allow different logos for different users/groups
4. **Version history:** Keep track of previous logos and allow rollbacks
5. **API documentation:** Complete API documentation for third-party integration
