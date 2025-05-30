﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dscui.Contracts.Services;
using dscui.Contracts.ViewModels;
using DSCUI.Services.DesiredStateConfiguration.Contracts;
using Microsoft.UI.Dispatching;
using Microsoft.Management.Configuration;
using System.Collections.ObjectModel;
using dscui.Models;

namespace dscui.ViewModels.ConfigurationFlow;

public partial class ApplyFileViewModel : ObservableRecipient, INavigationAware
{
    private readonly IStringResource _stringResource;
    private readonly IConfigurationNavigationService _navigationService;
    private readonly IDSC _dsc;
    private readonly DispatcherQueue _dq;
    private IDSCSet? _dscSet;

    [ObservableProperty]
    private bool _isLoading = true;

    public ObservableCollection<ApplySetUnit> Units { get; } = [];

    public ApplyFileViewModel(
        IConfigurationNavigationService navigationService,
        IDSC dsc,
        IStringResource stringResource)
    {
        _navigationService = navigationService;
        _dsc = dsc;
        _dq = DispatcherQueue.GetForCurrentThread();
        _stringResource = stringResource;
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is IDSCSet dscSet)
        {
            _dscSet = dscSet;
            foreach(var unit in dscSet.Units)
            {
                Units.Add(new(unit, _stringResource));
            }
        }
    }

    public void OnNavigatedFrom()
    {
        // No-op
    }

    [RelayCommand]
    private async Task OnLoadedAsync()
    {
        if(_dscSet != null)
        {
            var task = _dsc.ApplySetAsync(_dscSet);
            task.Progress = (_, data) => OnDataChanged(data);
            await task;
        }
    }

    private void OnDataChanged(IDSCSetChangeData data)
    {
        _dq.TryEnqueue(() =>
        {
            if (data.Change == ConfigurationSetChangeEventType.UnitStateChanged && data.Unit != null)
            {
                var unit = Units.FirstOrDefault(u => u.Unit.InstanceId == data.Unit.InstanceId);
                if (unit != null)
                {
                    if (data.UnitState == ConfigurationUnitState.InProgress)
                    {
                        unit.Update(ApplySetUnitState.InProgress);
                    }
                    else if (data.UnitState == ConfigurationUnitState.Skipped)
                    {
                        unit.Update(ApplySetUnitState.Skipped, data.ResultInformation);
                    }
                    else if (data.UnitState == ConfigurationUnitState.Completed)
                    {
                        var state = data.ResultInformation.IsOk ? ApplySetUnitState.Succeeded : ApplySetUnitState.Failed;
                        unit.Update(state, data.ResultInformation);
                    }
                }
            }
            else if (data.Change == ConfigurationSetChangeEventType.SetStateChanged && data.SetState == ConfigurationSetState.Completed)
            {
                IsLoading = false;
            }
        });
    }

    [RelayCommand]
    private async Task OnDoneAsync()
    {
        _navigationService.NavigateToDefaultPage();
        await Task.CompletedTask;
    }
}
