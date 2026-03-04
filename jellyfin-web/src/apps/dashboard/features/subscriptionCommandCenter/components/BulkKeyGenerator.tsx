import {
    AlertCircle,
    Check,
    Copy,
    Download,
    Loader2,
    Wand2
} from 'lucide-react';
import React, {
    type ChangeEvent,
    type MouseEvent,
    useCallback,
    useMemo,
    useState
} from 'react';

import { bulkGenerateKeys, type GeneratedKey } from '../data/api';
import { cn } from '../utils/cn';

interface DurationOption {
    label: string;
    months: number;
}

const DURATIONS: DurationOption[] = [
    { label: '1 Month', months: 1 },
    { label: '3 Months', months: 3 },
    { label: '6 Months', months: 6 },
    { label: '12 Months', months: 12 }
];

function exportCSV(keys: GeneratedKey[]): void {
    const header = 'Key,Duration,Prefix,Batch Name,Reseller Tag,Created At\n';
    const rows = keys.map((key) => (
        `${key.key},${key.duration},${key.prefix},${key.batchName},${key.resellerTag},${key.createdAt}`
    ));
    const csv = header + rows.join('\n');
    const anchor = document.createElement('a');

    anchor.href = `data:text/csv;charset=utf-8,${encodeURIComponent(csv)}`;
    anchor.download = `keys-${Date.now()}.csv`;
    anchor.click();
}

function copyTextToClipboard(value: string): boolean {
    const textarea = document.createElement('textarea');

    textarea.value = value;
    textarea.setAttribute('readonly', '');
    textarea.style.position = 'fixed';
    textarea.style.left = '-9999px';
    document.body.appendChild(textarea);
    textarea.select();

    let copied = false;

    try {
        copied = document.execCommand('copy');
    } finally {
        document.body.removeChild(textarea);
    }

    return copied;
}

const BulkKeyGenerator = () => {
    const [ durationMonths, setDurationMonths ] = useState(1);
    const [ quantity, setQuantity ] = useState(10);
    const [ prefix, setPrefix ] = useState('JF');
    const [ batchName, setBatchName ] = useState('');
    const [ resellerTag, setResellerTag ] = useState('');
    const [ loading, setLoading ] = useState(false);
    const [ generatedKeys, setGeneratedKeys ] = useState<GeneratedKey[]>([]);
    const [ copied, setCopied ] = useState(false);
    const [ error, setError ] = useState('');

    const selectedDuration = useMemo(
        () => DURATIONS.find((item) => item.months === durationMonths)?.label || `${durationMonths} Months`,
        [ durationMonths ]
    );

    const onGenerate = useCallback(async () => {
        if (!batchName.trim()) {
            setError('Batch Name is required.');
            return;
        }

        if (quantity < 1 || quantity > 1000) {
            setError('Quantity must be between 1 and 1000.');
            return;
        }

        setError('');
        setLoading(true);

        try {
            const keys = await bulkGenerateKeys({
                durationMonths,
                quantity,
                prefix,
                batchName,
                resellerTag
            });
            setGeneratedKeys(keys);
        } catch (errorValue: unknown) {
            let message = 'Failed to generate keys. Please try again.';

            if (errorValue instanceof Error && errorValue.message) {
                message = `Failed to generate keys: ${errorValue.message}`;
            }

            setError(message);
        } finally {
            setLoading(false);
        }
    }, [ batchName, durationMonths, prefix, quantity, resellerTag ]);

    const onCopy = useCallback(() => {
        if (!generatedKeys.length) {
            return;
        }

        try {
            const copiedValue = copyTextToClipboard(generatedKeys.map((key) => key.key).join('\n'));

            if (!copiedValue) {
                throw new Error('Copy command failed.');
            }

            setCopied(true);
            window.setTimeout(() => setCopied(false), 2000);
        } catch (errorValue: unknown) {
            let message = 'Unable to copy keys to clipboard.';

            if (errorValue instanceof Error && errorValue.message) {
                message = `Unable to copy keys: ${errorValue.message}`;
            }

            setError(message);
        }
    }, [ generatedKeys ]);

    const onDurationClick = useCallback((event: MouseEvent<HTMLButtonElement>) => {
        const months = Number(event.currentTarget.dataset.months);

        if (!Number.isNaN(months)) {
            setDurationMonths(months);
        }
    }, []);

    const onQuantityChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
        const next = Number(event.target.value);

        if (Number.isNaN(next)) {
            setQuantity(1);
            return;
        }

        setQuantity(Math.max(1, Math.min(1000, next)));
    }, []);

    const onPrefixChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
        setPrefix(event.target.value.toUpperCase());
    }, []);

    const onBatchNameChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
        setBatchName(event.target.value);
    }, []);

    const onResellerTagChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
        setResellerTag(event.target.value);
    }, []);

    const onGenerateClick = useCallback(() => {
        void onGenerate();
    }, [ onGenerate ]);

    const onDownloadClick = useCallback(() => {
        exportCSV(generatedKeys);
    }, [ generatedKeys ]);

    return (
        <section className='scc-section scc-card'>
            <h2 className='scc-sectionTitle'>
                <span className='scc-sectionAccent scc-accent-sky' />
                Bulk Key Generator
            </h2>

            <div className='scc-formGrid'>
                <div className='scc-formField scc-colSpan2'>
                    <label htmlFor='scc-duration' className='scc-label'>Duration</label>
                    <div id='scc-duration' className='scc-durationGroup' role='group' aria-label='Subscription duration'>
                        {DURATIONS.map((option) => (
                            <button
                                key={option.months}
                                type='button'
                                className={cn(
                                    'scc-durationButton',
                                    option.months === durationMonths && 'scc-durationButtonActive'
                                )}
                                data-months={option.months}
                                onClick={onDurationClick}
                            >
                                {option.label}
                            </button>
                        ))}
                    </div>
                </div>

                <div className='scc-formField'>
                    <label htmlFor='scc-quantity' className='scc-label'>Quantity</label>
                    <input
                        id='scc-quantity'
                        type='number'
                        min={1}
                        max={1000}
                        className='scc-input'
                        value={quantity}
                        onChange={onQuantityChange}
                    />
                </div>

                <div className='scc-formField'>
                    <label htmlFor='scc-prefix' className='scc-label'>Prefix</label>
                    <input
                        id='scc-prefix'
                        type='text'
                        className='scc-input'
                        value={prefix}
                        onChange={onPrefixChange}
                    />
                </div>

                <div className='scc-formField'>
                    <label htmlFor='scc-batch-name' className='scc-label'>
                        Batch Name
                        <span className='scc-required'>*</span>
                    </label>
                    <input
                        id='scc-batch-name'
                        type='text'
                        className='scc-input'
                        value={batchName}
                        onChange={onBatchNameChange}
                    />
                </div>

                <div className='scc-formField scc-colSpan2'>
                    <label htmlFor='scc-reseller-tag' className='scc-label'>Reseller Tag</label>
                    <input
                        id='scc-reseller-tag'
                        type='text'
                        className='scc-input'
                        value={resellerTag}
                        onChange={onResellerTagChange}
                    />
                </div>
            </div>

            {error && (
                <div className='scc-formError'>
                    <AlertCircle width={14} height={14} />
                    <span>{error}</span>
                </div>
            )}

            <p className='scc-noteText'>
                Keys are generated through server API and saved in DB. Prefix, Batch Name, and Reseller Tag are export labels only.
            </p>

            <div className='scc-formActions'>
                <button
                    type='button'
                    className='scc-primaryButton'
                    disabled={loading}
                    onClick={onGenerateClick}
                >
                    {loading ? (
                        <>
                            <Loader2 width={14} height={14} className='scc-spin' />
                            Generating {quantity} keys...
                        </>
                    ) : (
                        <>
                            <Wand2 width={14} height={14} />
                            Generate Keys
                        </>
                    )}
                </button>

                {generatedKeys.length > 0 && (
                    <div className='scc-secondaryActions'>
                        <button
                            type='button'
                            className='scc-secondaryButton scc-secondaryButtonEmerald'
                            onClick={onDownloadClick}
                        >
                            <Download width={14} height={14} />
                            Download CSV
                        </button>
                        <button
                            type='button'
                            className='scc-secondaryButton scc-secondaryButtonViolet'
                            onClick={onCopy}
                        >
                            {copied ? <Check width={14} height={14} /> : <Copy width={14} height={14} />}
                            {copied ? 'Copied' : 'Copy Keys'}
                        </button>
                    </div>
                )}
            </div>

            {generatedKeys.length > 0 && (
                <div className='scc-keyList'>
                    <div className='scc-keyListHeader'>
                        <span>Generated Keys ({generatedKeys.length})</span>
                        <span>Batch: {batchName} - {selectedDuration}</span>
                    </div>
                    <div className='scc-keyListGrid'>
                        {generatedKeys.map((key) => (
                            <span key={key.key} className='scc-keyItem'>
                                {key.key}
                            </span>
                        ))}
                    </div>
                </div>
            )}
        </section>
    );
};

export default BulkKeyGenerator;
