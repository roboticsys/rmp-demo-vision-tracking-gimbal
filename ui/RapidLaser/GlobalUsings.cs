// system
global using System;
global using System.Text;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.IO;
global using System.Linq;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Diagnostics;
global using System.Timers;
global using System.Collections.ObjectModel;
global using System.Text.Json;
global using System.Net.Http;
global using System.Reflection;
global using System.Globalization;

// avalonia
global using Avalonia;
global using Avalonia.Input;
global using Avalonia.Data.Converters;
global using Avalonia.Markup.Xaml;
global using Avalonia.Media;
global using Avalonia.Media.Imaging;
global using Avalonia.Platform;
global using Avalonia.Threading;
global using Avalonia.Controls;
global using Avalonia.Controls.Templates;
global using Avalonia.Controls.Primitives;
global using Avalonia.Controls.ApplicationLifetimes;
global using Avalonia.Data.Core.Plugins;

// rsi
global using RSI.RapidServer;
global using RSI.RapidCodeRemote;
global using static RSI.RapidServer.ServerControlService;

// third parties
global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;
global using Renci.SshNet;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;

// gRPC
global using Grpc.Net.Client;
global using Grpc.Core;

// project  
global using RapidLaser.Services;
global using RapidLaser.ViewModels;
global using RapidLaser.Controls;
global using RapidLaser.Models;
global using RapidLaser.Views;