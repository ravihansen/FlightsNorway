﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

using FlightsNorway.Model;
using FlightsNorway.Services;
using FlightsNorway.Messages;
using FlightsNorway.Extensions;
using FlightsNorway.FlightDataServices;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;

using Microsoft.Phone.Shell;
using Microsoft.Phone.Reactive;

namespace FlightsNorway.ViewModels
{
    public interface IFlightsViewModel
    {
        ObservableCollection<Flight> Arrivals { get; }
        ObservableCollection<Flight> Departures { get; }
        Airport SelectedAirport { get; set; }
    }

    public class FlightsViewModel : ViewModelBase, IFlightsViewModel
    {
        private readonly IPhoneApplicationService _appService;
        private readonly IGetFlights _flightsService;
        private readonly IStoreObjects _objectStore;

        public ObservableCollection<Flight> Arrivals { get; private set; }
        public ObservableCollection<Flight> Departures { get; private set; }
        public string ErrorMessage { get; private set; }

        private Airport _selectedAirport;

        public Airport SelectedAirport
        {
            get { return _selectedAirport; }
            set
            {
                if (_selectedAirport == value) return;

                _selectedAirport = value;
                LoadFlightsFromAirport(_selectedAirport);
                RaisePropertyChanged("SelectedAirport");
            }
        }       

        public FlightsViewModel(IGetFlights flightsService, IStoreObjects objectStore, IPhoneApplicationService appService)
        {
            Debug.WriteLine("FlightsViewModel Constructor");

            Arrivals = new ObservableCollection<Flight>();
            Departures = new ObservableCollection<Flight>();

            _objectStore = objectStore;
            _flightsService = flightsService;
            
            _appService = appService;
            _appService.Deactivated += OnDeactivated;

            Messenger.Default.Register<AirportSelectedMessage>(this, OnAirportSelected);

            LoadSelectedAirport();
            LoadFlightsFromAppState();
        }

        private void LoadFlightsFromAppState()
        {
            if(_appService.State.ContainsKey("Arrivals") && _appService.State.ContainsKey("Departures"))
            {
                Arrivals.AddRange(_appService.State.Get<ObservableCollection<Flight>>("Arrivals"));
                Departures.AddRange(_appService.State.Get<ObservableCollection<Flight>>("Departures"));
            }
        }

        private void OnDeactivated(object sender, DeactivatedEventArgs e)
        {
            _appService.State.AddOrReplace("Arrivals", Arrivals);
            _appService.State.AddOrReplace("Departures", Departures);
            _appService.State.AddOrReplace("SelectedAirport", SelectedAirport);
        }

        private void LoadSelectedAirport()
        {
            if(_appService.State.ContainsKey("SelectedAirport"))
            {
                _selectedAirport = _appService.State.Get<Airport>("SelectedAirport");
            }
            else if (_objectStore.FileExists(ObjectStore.SelectedAirportFilename))
            {
                var airport = _objectStore.Load<Airport>(ObjectStore.SelectedAirportFilename);
                if (airport.Equals(Airport.Nearest))
                {
                    Messenger.Default.Send(new FindNearestAirportMessage());
                }
                else
                {
                    SelectedAirport = airport;
                }
            }
        }

        private void OnAirportSelected(AirportSelectedMessage message)
        {
            SelectedAirport = message.Content;            
        }

        public void LoadFlightsFromAirport(Airport airport)
        {
            Arrivals.Clear();
            Departures.Clear();

            var flights = _flightsService.GetFlightsFrom(_selectedAirport);      
            flights.Subscribe(LoadFlights, HandleException);            
        }

        private void LoadFlights(IEnumerable<Flight> flights)
        {
            foreach(var flight in flights)
            {
                double hoursSince = DateTime.Now.Subtract(flight.ScheduledTime).TotalHours;
                               
                if(flight.Direction == Direction.Arrival)
                {
                    //if (hoursSince > 1) continue;
                    Arrivals.Add(flight);
                }
                else
                {
                    //if (hoursSince > 0.25) continue;                    
                    Departures.Add(flight);                        
                }
            }
        }

        private void HandleException(Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}