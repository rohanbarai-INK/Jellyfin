import './loading.scss';

type LoaderType = 'system' | 'media';

let loader: HTMLDivElement | undefined;
let loaderImage: HTMLImageElement | undefined;
let currentLoaderType: LoaderType = 'media';

function getLoaderSource(type: LoaderType): string {
    if (type === 'system') {
        return 'assets/branding/system-loader.gif';
    }

    return 'assets/branding/media-loader.gif';
}

function createLoader(type: LoaderType): HTMLDivElement {
    const container = document.createElement('div');
    container.setAttribute('dir', 'ltr');
    container.classList.add('docspinner');
    container.classList.add('gif-loader');

    const img = document.createElement('img');
    img.classList.add('loaderImage');
    img.src = getLoaderSource(type);

    loaderImage = img;
    container.appendChild(img);
    document.body.appendChild(container);

    return container;
}

export function show(type: LoaderType = 'media') {
    currentLoaderType = type;
    const source = getLoaderSource(currentLoaderType);

    if (!loader) {
        loader = createLoader(currentLoaderType);
    } else {
        const image = loaderImage || loader.querySelector('.loaderImage');

        if (image instanceof HTMLImageElement) {
            loaderImage = image;
        }
    }

    if (loaderImage) {
        loaderImage.src = source;
    }

    loader.classList.add('mdlSpinnerActive');
}

export function hide() {
    if (loader) {
        loader.classList.remove('mdlSpinnerActive');
    }
}

const loading = {
    show,
    hide
};

window.Loading = loading;

export default loading;
