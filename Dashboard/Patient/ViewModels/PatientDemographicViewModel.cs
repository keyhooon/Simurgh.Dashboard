using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simurgh.Dashboard.Patient.Contracts;
using Simurgh.Dashboard.Patient.Models;

namespace Simurgh.Dashboard.Patient.ViewModels;

/// <summary>
/// Bridges the Patient Demographic Domain Model/Controller and the UI Presentation State.
/// Coordinates data bindings, culture-aware formatting, clinical age categorization, and effective visibility.
/// </summary>
public sealed class PatientDemographicViewModel : ObservableObject, IDisposable
{
    private const string MissingValue = "--";

    private static readonly CultureInfo PersianCulture = CreatePersianCulture();

    private static readonly string[] DemographicPropertyNames =
    [
        nameof(DemographicModel),
        nameof(Payload),
        nameof(HasPatient),
        nameof(PatientId),
        nameof(FullName),
        nameof(DateOfBirth),
        nameof(Age),
        nameof(Sex),
        nameof(ScheduledProcedureDescription),
        nameof(PerformedPhysician),
        nameof(AccessionNumber),
        nameof(SpecialNeeds),
        nameof(MedicalAlert),
        nameof(PatientComment),
        nameof(ContrastAllergies),
        nameof(FormattedAge),
        nameof(SexBadge),
        nameof(FormattedDateOfBirth),
        nameof(ProcedureDisplay),
        nameof(PhysicianDisplay),
        nameof(AccessionDisplay),
        nameof(SpecialNeedsDisplay),
        nameof(MedicalAlertDisplay),
        nameof(PatientCommentDisplay),
        nameof(ContrastAllergiesDisplay),
        nameof(HasSpecialNeeds),
        nameof(HasMedicalAlert),
        nameof(HasPatientComment),
        nameof(HasContrastAllergies),
        nameof(HasClinicalInformation)
    ];

    private static readonly string[] VisibilityPropertyNames =
    [
        nameof(IsPatientIdVisible),
        nameof(IsFullNameVisible),
        nameof(IsDateOfBirthVisible),
        nameof(IsAgeVisible),
        nameof(IsSexVisible),
        nameof(IsProcedureVisible),
        nameof(IsPhysicianVisible),
        nameof(IsAccessionNumberVisible),
        nameof(IsSpecialNeedsVisible),
        nameof(IsMedicalAlertVisible),
        nameof(IsPatientCommentVisible),
        nameof(IsContrastAllergiesVisible),
        nameof(HasDemographicVisibility),
        nameof(HasProcedureVisibility),
        nameof(HasClinicalVisibility),
        nameof(IsClinicalOrProcedureVisible),
        nameof(IsAnyPropertyVisible)
    ];

    private readonly IPatientDemographicController _controller;
    private readonly IPatientUiAccessor _uiAccessor;
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    public PatientDemographicViewModel(
        IPatientDemographicController controller,
        IPatientUiAccessor uiAccessor,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(uiAccessor);

        _controller = controller;
        _uiAccessor = uiAccessor;
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Subscribe to Domain Entity and UI Accessor property change notifications
        _controller.PatientDemographicEntity.PropertyChanged += OnDemographicEntityPropertyChanged;
        _uiAccessor.PropertyChanged += OnUiAccessorPropertyChanged;
    }

    #region Models & Controller Exposure

    public PatientDemographicEntity DemographicModel => _controller.PatientDemographicEntity;
    public PatientUiEntity UiModel => _uiAccessor.CurrentUiEntity;
    public PatientDemographicPayload? Payload => DemographicModel.PatientDemographic;
    public bool HasPatient => DemographicModel.HasValue && Payload is not null;



    #endregion

    #region Raw Demographic Properties

    public string PatientId => Payload?.PatientId ?? string.Empty;
    public string FullName => Payload?.FullName ?? string.Empty;
    public DateTime? DateOfBirth => Payload?.DateOfBirth;
    public int? Age => Payload?.Age;
    public BiologicalSex Sex => Payload?.Sex ?? BiologicalSex.Unknown;
    public string ScheduledProcedureDescription => Payload?.ScheduledProcedureDescription ?? string.Empty;
    public string PerformedPhysician => Payload?.PerformedPhysician ?? string.Empty;
    public string AccessionNumber => Payload?.AccessionNumber ?? string.Empty;

    public string SpecialNeeds => Payload?.SpecialNeeds ?? string.Empty;
    public string MedicalAlert => Payload?.MedicalAlert ?? string.Empty;
    public string PatientComment => Payload?.PatientComment ?? string.Empty;
    public string ContrastAllergies => Payload?.ContrastAllergies ?? string.Empty;

    #endregion

    #region Theme & Brushes (from PatientUiEntity)

    public Brush PrimaryBrush => UiModel.PrimaryBrush;
    public Brush SecondaryBrush => UiModel.SecondaryBrush;

    #endregion

    #region Presentation & Calculated Properties

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    public string SexBadge => Sex switch
    {
        BiologicalSex.Male => "Male",
        BiologicalSex.Female => "Female",
        BiologicalSex.Other => "Other",
        _ => "Unknown"
    };

    public string FormattedAge => FormatAge(Today);

    public string FormattedDateOfBirth
    {
        get
        {
            if (DateOfBirth is not { } birthDate)
            {
                return MissingValue;
            }

            var calendar = PersianCulture.DateTimeFormat.Calendar;
            if (birthDate < calendar.MinSupportedDateTime || birthDate > calendar.MaxSupportedDateTime)
            {
                return MissingValue;
            }

            return birthDate.ToString("d MMMM yyyy", PersianCulture);
        }
    }

    public string ProcedureDisplay => DisplayOrPlaceholder(ScheduledProcedureDescription);
    public string PhysicianDisplay => DisplayOrPlaceholder(PerformedPhysician);
    public string AccessionDisplay => DisplayOrPlaceholder(AccessionNumber);
    public string SpecialNeedsDisplay => DisplayOrPlaceholder(SpecialNeeds);
    public string MedicalAlertDisplay => DisplayOrPlaceholder(MedicalAlert);
    public string PatientCommentDisplay => DisplayOrPlaceholder(PatientComment);
    public string ContrastAllergiesDisplay => DisplayOrPlaceholder(ContrastAllergies);

    public bool HasSpecialNeeds => HasText(SpecialNeeds);
    public bool HasMedicalAlert => HasText(MedicalAlert);
    public bool HasPatientComment => HasText(PatientComment);
    public bool HasContrastAllergies => HasText(ContrastAllergies);

    public bool HasClinicalInformation =>
        HasSpecialNeeds ||
        HasMedicalAlert ||
        HasPatientComment ||
        HasContrastAllergies;

    #endregion

    #region Effective Visibility (Config Preference ∧ Data Availability)

    public bool IsPatientIdVisible =>
        HasPatient && UiModel.IsPatientIdVisible && HasText(PatientId);

    public bool IsFullNameVisible =>
        HasPatient && UiModel.IsFullNameVisible && HasText(FullName);

    public bool IsDateOfBirthVisible =>
        HasPatient && UiModel.IsDateOfBirthVisible && DateOfBirth.HasValue;

    public bool IsAgeVisible =>
        HasPatient && UiModel.IsAgeVisible && HasDisplayableAge(Today);

    public bool IsSexVisible =>
        HasPatient && UiModel.IsSexVisible && Sex is BiologicalSex.Male or BiologicalSex.Female or BiologicalSex.Other;

    public bool IsProcedureVisible =>
        HasPatient && UiModel.IsProcedureVisible && HasText(ScheduledProcedureDescription);

    public bool IsPhysicianVisible =>
        HasPatient && UiModel.IsPhysicianVisible && HasText(PerformedPhysician);

    public bool IsAccessionNumberVisible =>
        HasPatient && UiModel.IsAccessionNumberVisible && HasText(AccessionNumber);

    public bool IsSpecialNeedsVisible =>
        HasPatient && UiModel.IsSpecialNeedsVisible && HasSpecialNeeds;

    public bool IsMedicalAlertVisible =>
        HasPatient && UiModel.IsMedicalAlertVisible && HasMedicalAlert;

    public bool IsPatientCommentVisible =>
        HasPatient && UiModel.IsPatientCommentVisible && HasPatientComment;

    public bool IsContrastAllergiesVisible =>
        HasPatient && UiModel.IsContrastAllergiesVisible && HasContrastAllergies;

    public bool HasDemographicVisibility =>
        IsPatientIdVisible ||
        IsFullNameVisible ||
        IsDateOfBirthVisible ||
        IsAgeVisible ||
        IsSexVisible;

    public bool HasProcedureVisibility =>
        IsProcedureVisible ||
        IsPhysicianVisible;

    public bool HasClinicalVisibility =>
        IsSpecialNeedsVisible ||
        IsMedicalAlertVisible ||
        IsPatientCommentVisible ||
        IsContrastAllergiesVisible;

    public bool IsClinicalOrProcedureVisible =>
        HasClinicalVisibility || HasProcedureVisibility;

    public bool IsAnyPropertyVisible =>
        HasDemographicVisibility ||
        HasProcedureVisibility ||
        HasClinicalVisibility;

    #endregion

    #region Age Calculation Logic

    private bool HasDisplayableAge(DateOnly today)
    {
        if (DateOfBirth is { } birthDate)
        {
            return DateOnly.FromDateTime(birthDate) <= today;
        }

        return Age is >= 0;
    }

    private string FormatAge(DateOnly today)
    {
        if (DateOfBirth is not { } dateOfBirth)
        {
            return Age is >= 0 ? $"{Age.Value} سال" : MissingValue;
        }

        var birthDate = DateOnly.FromDateTime(dateOfBirth);
        if (birthDate > today)
        {
            return MissingValue;
        }

        int years = today.Year - birthDate.Year;
        if (birthDate.AddYears(years) > today)
        {
            years--;
        }

        if (years >= 1)
        {
            return $"{years} سال";
        }

        int months = ((today.Year - birthDate.Year) * 12) + today.Month - birthDate.Month;
        if (birthDate.AddMonths(months) > today)
        {
            months--;
        }

        if (months >= 1)
        {
            return $"{months} ماه";
        }

        int days = today.DayNumber - birthDate.DayNumber;
        return $"{days} روز";
    }

    public void RefreshDateDependentProperties()
    {
        ThrowIfDisposed();

        OnPropertyChanged(nameof(FormattedAge));
        OnPropertyChanged(nameof(IsAgeVisible));
        OnPropertyChanged(nameof(HasDemographicVisibility));
        OnPropertyChanged(nameof(IsAnyPropertyVisible));
    }

    #endregion

    #region Event Handlers

    private void OnDemographicEntityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;

        NotifyProperties(DemographicPropertyNames);
        NotifyProperties(VisibilityPropertyNames);

        // Synchronize command guard status in case controller didn't trigger it directly
    }

    private void OnUiAccessorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;

        switch (e.PropertyName)
        {
            case nameof(PatientUiEntity.PrimaryBrush):
                OnPropertyChanged(nameof(PrimaryBrush));
                break;

            case nameof(PatientUiEntity.SecondaryBrush):
                OnPropertyChanged(nameof(SecondaryBrush));
                break;

            case nameof(IPatientUiAccessor.CurrentUiEntity):
                OnPropertyChanged(nameof(UiModel));
                OnPropertyChanged(nameof(PrimaryBrush));
                OnPropertyChanged(nameof(SecondaryBrush));
                NotifyProperties(VisibilityPropertyNames);
                break;

            default:
                // Any visibility configuration toggle change invalidates effective visibility
                NotifyProperties(VisibilityPropertyNames);
                break;
        }
    }

    private void NotifyProperties(string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    #endregion

    #region Mutation Delegation

    public void UpdateDemographics(PatientDemographicPayload payload)
    {
        ThrowIfDisposed();
    }

    public void Reset()
    {
        ThrowIfDisposed();
    }

    #endregion

    #region Helpers & Cleanup

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static string DisplayOrPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ? MissingValue : value;

    private static CultureInfo CreatePersianCulture()
    {
        var culture = new CultureInfo("fa-IR");
        culture.DateTimeFormat.Calendar = new PersianCalendar();
        return CultureInfo.ReadOnly(culture);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _controller.PatientDemographicEntity.PropertyChanged -= OnDemographicEntityPropertyChanged;
        _uiAccessor.PropertyChanged -= OnUiAccessorPropertyChanged;
    }

    #endregion
}
