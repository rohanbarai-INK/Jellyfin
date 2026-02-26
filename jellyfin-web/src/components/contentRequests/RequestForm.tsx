import React, { type FC, type FormEvent } from 'react';

import globalize from 'lib/globalize';
import { type ContentRequestType } from 'utils/contentRequestsApi';

import useRequestIsMobileLayout from './useRequestIsMobileLayout';

interface RequestFormProps {
    requestType: ContentRequestType
    title: string
    seasonNumber: string
    isSubmitting: boolean
    isSubmitEnabled: boolean
    isTypeSelectionEnabled: boolean
    isVisible: boolean
    remainingMovies: number
    remainingSeries: number
    message: string
    isMessageError: boolean
    onRequestTypeChange: (value: ContentRequestType) => void
    onTitleChange: (value: string) => void
    onSeasonNumberChange: (value: string) => void
    onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

const RequestForm: FC<RequestFormProps> = ({
    requestType,
    title,
    seasonNumber,
    isSubmitting,
    isSubmitEnabled,
    isTypeSelectionEnabled,
    isVisible,
    remainingMovies,
    remainingSeries,
    message,
    isMessageError,
    onRequestTypeChange,
    onTitleChange,
    onSeasonNumberChange,
    onSubmit
}) => {
    const isMobileLayout = useRequestIsMobileLayout();

    if (!isVisible) {
        return null;
    }

    const areInputsDisabled = !isSubmitEnabled || isSubmitting;
    const isToggleDisabled = !isTypeSelectionEnabled || isSubmitting;
    const movieToggleLabel = isMobileLayout
        ? globalize.translate('Movies')
        : globalize.translate('TypeOptionPluralMovie');
    const seriesToggleLabel = isMobileLayout
        ? globalize.translate('Series')
        : globalize.translate('TypeOptionPluralSeries');

    return (
        <form className='requestFormGrid' onSubmit={onSubmit}>
            <div className='requestTypeToggle requestFormFullWidth' role='radiogroup' aria-label={globalize.translate('RequestTypeLabel')}>
                <button
                    type='button'
                    aria-pressed={requestType === 'Movie'}
                    onClick={() => onRequestTypeChange('Movie')}
                    disabled={isToggleDisabled}
                >
                    {movieToggleLabel}
                </button>
                <button
                    type='button'
                    aria-pressed={requestType === 'Series'}
                    onClick={() => onRequestTypeChange('Series')}
                    disabled={isToggleDisabled}
                >
                    {seriesToggleLabel}
                </button>
            </div>

            <div className='requestCaps requestFormFullWidth'>
                <span className='requestCapPill'>
                    {globalize.translate('RequestRemainingMovies', remainingMovies)}
                </span>
                <span className='requestCapPill'>
                    {globalize.translate('RequestRemainingSeries', remainingSeries)}
                </span>
            </div>

            <input
                className='requestInput requestFormFullWidth'
                type='text'
                maxLength={255}
                value={title}
                disabled={areInputsDisabled}
                onChange={event => onTitleChange(event.target.value)}
                placeholder={globalize.translate('RequestTitlePlaceholder')}
                aria-label={globalize.translate('RequestTitleLabel')}
            />

            {requestType === 'Series' && (
                <input
                    className='requestInput requestFormFullWidth'
                    type='number'
                    min={1}
                    value={seasonNumber}
                    disabled={areInputsDisabled}
                    onChange={event => onSeasonNumberChange(event.target.value)}
                    placeholder={globalize.translate('RequestSeasonPlaceholder')}
                    aria-label={globalize.translate('RequestSeasonLabel')}
                />
            )}

            <div className='requestSubmitRow requestFormFullWidth'>
                <button
                    className='requestSubmitButton'
                    type='submit'
                    disabled={!isSubmitEnabled || isSubmitting}
                >
                    {isSubmitting
                        ? globalize.translate('RequestSubmitting')
                        : globalize.translate('ButtonSubmit')}
                </button>
                {!!message && (
                    <span className={`requestFormMessage${isMessageError ? ' error' : ''}`}>
                        {message}
                    </span>
                )}
            </div>
        </form>
    );
};

export default RequestForm;
