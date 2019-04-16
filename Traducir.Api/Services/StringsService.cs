using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Traducir.Api.ViewModels.Strings;
using Traducir.Core.Helpers;
using Traducir.Core.Models;
using Traducir.Core.Models.Enums;
using Traducir.Core.Services;

namespace Traducir.Api.Services
{
    public interface IStringsService
    {
        Task<ImmutableArray<SOString>> Query(QueryViewModel model);

        Task<StringCountsViewModel> GetStringCounts();
    }

    public class StringsService : IStringsService
    {
        private readonly ISOStringService _soStringService;

        public StringsService(ISOStringService soStringService)
        {
            this._soStringService = soStringService;
        }

        public async Task<ImmutableArray<SOString>> Query(QueryViewModel model)
        {
            Func<SOString, bool> predicate = null;

            void ComposePredicate(Func<SOString, bool> newPredicate)
            {
                if (predicate == null)
                {
                    predicate = newPredicate;
                    return;
                }

                var oldPredicate = predicate;
                predicate = s => oldPredicate(s) && newPredicate(s);
            }

            if (model.TranslationStatus != TranslationStatus.AnyStatus)
            {
                ComposePredicate(s => s.HasTranslation == (model.TranslationStatus == TranslationStatus.WithTranslation));
            }

            if (model.PushStatus != PushStatus.AnyStatus)
            {
                ComposePredicate(s => s.NeedsPush == (model.PushStatus == PushStatus.NeedsPush));
            }

            if (model.UrgencyStatus != UrgencyStatus.AnyStatus)
            {
                ComposePredicate(s => s.IsUrgent == (model.UrgencyStatus == UrgencyStatus.IsUrgent));
            }

            if (model.IgnoredStatus != IgnoredStatus.IncludeIgnored)
            {
                ComposePredicate(s => s.IsIgnored == (model.IgnoredStatus == IgnoredStatus.OnlyIgnored));
            }

            if (model.SuggestionsStatus != SuggestionApprovalStatus.AnyStatus)
            {
                switch (model.SuggestionsStatus)
                {
                    case SuggestionApprovalStatus.DoesNotHaveSuggestions:
                        ComposePredicate(s => !s.HasSuggestions);
                        break;
                    case SuggestionApprovalStatus.HasSuggestionsNeedingReview:
                        ComposePredicate(s =>
                            s.Suggestions != null &&
                            s.Suggestions.Any(sug => sug.State == StringSuggestionState.Created || sug.State == StringSuggestionState.ApprovedByTrustedUser));
                        break;
                    case SuggestionApprovalStatus.HasSuggestionsNeedingApproval:
                        ComposePredicate(s => s.HasSuggestionsWaitingApproval);
                        break;
                    case SuggestionApprovalStatus.HasSuggestionsNeedingReviewApprovedByTrustedUser:
                        ComposePredicate(s => s.HasApprovedSuggestionsWaitingReview);
                        break;
                }
            }

            if (model.Key.HasValue())
            {
                ComposePredicate(s => s.Key.StartsWith(model.Key, true, CultureInfo.InvariantCulture));
            }

            if (model.SourceRegex.HasValue())
            {
                Regex regex;
                try
                {
                    regex = new Regex(model.SourceRegex, RegexOptions.Compiled);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException("Invalid source regex", ex);
                }

                ComposePredicate(s => regex.IsMatch(s.OriginalString));
            }

            if (model.TranslationRegex.HasValue())
            {
                Regex regex;
                try
                {
                    regex = new Regex(model.TranslationRegex, RegexOptions.Compiled);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException("Invalid translation regex", ex);
                }

                ComposePredicate(s => s.HasTranslation && regex.IsMatch(s.Translation));
            }

            return await _soStringService.GetStringsAsync(predicate);
        }

        public async Task<StringCountsViewModel> GetStringCounts()
        {
            return new StringCountsViewModel
            {
                TotalStrings = await _soStringService.CountStringsAsync(s => !s.IsIgnored),
                WithoutTranslation = await _soStringService.CountStringsAsync(s => !s.HasTranslation && !s.IsIgnored),
                WithPendingSuggestions = await _soStringService.CountStringsAsync(s => s.HasSuggestions && !s.IsIgnored),
                WaitingApproval = await _soStringService.CountStringsAsync(s => s.HasSuggestionsWaitingApproval && !s.IsIgnored),
                WaitingReview = await _soStringService.CountStringsAsync(s => s.HasApprovedSuggestionsWaitingReview && !s.IsIgnored),
                UrgentStrings = await _soStringService.CountStringsAsync(s => s.IsUrgent && !s.IsIgnored),
            };
        }
    }
}
