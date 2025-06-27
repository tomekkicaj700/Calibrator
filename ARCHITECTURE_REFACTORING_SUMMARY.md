# Calibrator Project Architecture Summary

## Overview

This document summarizes the architecture of the Calibrator WPF application, focusing on the main layers, service classes, logging, and configuration management. It is intended for developer onboarding and as a reference for future maintenance and refactoring.

---

## High-Level Architecture Diagram

```
WPF UI (MainWindow, UserControls)
   │
   ├── uses ──▶ WelderService (business logic)
   │                │
   │                ├── uses ──▶ Welder (domain model)
   │                │                │
   │                │                └── uses ──▶ WelderCommunicationService (communication abstraction)
   │                │                                 │
   │                │                                 ├── uses ──▶ SerialWelderCommunication (RS-232)
   │                │                                 └── uses ──▶ TcpWelderCommunication (USR-N520 TCP/IP)
   │                │
   │                └── uses ──▶ ConfigService (singleton, configuration & detected ports)
   │
   └── uses ──▶ LoggerService (singleton, logging)
```

---

## Layers and Responsibilities

### 1. **WPF UI Layer**
- **Files:** `MainWindow.xaml`, `MainWindow.xaml.cs`, UserControls (e.g., `CurrentCoefficients.xaml`)
- **Responsibilities:**
  - Handles user interaction and data binding.
  - Subscribes to events from service classes (e.g., `WelderService`, `ConfigService`).
  - Updates UI in response to data/events.

### 2. **Service Layer**
- **Key Classes:**
  - `WelderService` / `WelderServiceRefactored`: Main business logic for calibration, device scanning, parameter reading, and history management.
  - `ConfigService`: Centralized configuration and detected ports management (singleton, event-driven, JSON persistence).
  - `ServiceContainer`: Dependency injection and singleton management for services.
- **Responsibilities:**
  - Encapsulate business logic and device operations.
  - Provide async methods for device communication and configuration.
  - Expose events for UI updates.

### 3. **Domain/Model Layer**
- **Key Classes:**
  - `Welder`: Represents the welding device and its state.
  - `WelderSettings`, `DetectedPort`, `SKonfiguracjaSystemu`, etc.: Data models for configuration and device state.

### 4. **Communication Layer**
- **Key Classes:**
  - `WelderCommunicationService`: Abstracts communication (serial or TCP/IP) for the welder.
  - `SerialWelderCommunication`, `TcpWelderCommunication`: Implementations for specific protocols.
  - `WelderCommunicationFactory`: Factory for creating communication objects based on settings.
- **Responsibilities:**
  - Provide a unified async API for sending/receiving commands to hardware.
  - Hide protocol-specific details from higher layers.

### 5. **Logger**
- **Key Class:** `LoggerService` (singleton)
- **Usage:**
  - Use `LoggerService.Log("message")` to log events, errors, and diagnostics.
  - Logs are written asynchronously to `log.txt` and broadcast to the UI via events.
  - UI subscribes to `LogMessageAppended` and `LogHistoryLoaded` for real-time and historical log display.

---

## Configuration Management
- **ConfigService** is a singleton responsible for:
  - Loading/saving configuration (JSON, e.g., `welder_settings.json`).
  - Managing detected ports and communication types.
  - Broadcasting changes via events (`SettingsChanged`, `DetectedPortsChanged`).
  - Accessible from all layers (UI, services, communication).
- **WindowSettings** manages window/UI layout persistence (`window_settings.json`).

---

## Event Flow Example
1. **User clicks "Scan Ports"** in the UI.
2. UI calls `WelderService.ScanComPortsAsync()`.
3. `WelderService` uses `WelderCommunicationService` to scan hardware.
4. Results are sent to `ConfigService`, which updates detected ports and fires `DetectedPortsChanged`.
5. UI updates the list of available ports.
6. All actions and errors are logged via `LoggerService.Log()` and shown in the UI log panel.

---

## Logging
- **How to log:**
  - Call `LoggerService.Log("message")` from any layer.
  - Logs are timestamped and written to `log.txt`.
  - UI receives log updates via events for real-time display.
- **Log file:** `log.txt` in the project root.

---

## Adding New Features
- Add new device types: Implement a new `IWelderCommunication` and register in `WelderCommunicationFactory`.
- Add new configuration options: Extend `WelderSettings` and update `ConfigService`.
- Add new UI features: Bind to service events and update XAML/UI logic.

---

## Best Practices
- Use async/await for all hardware and file operations.
- Use events for UI updates (never block the UI thread).
- Centralize configuration and logging for maintainability.
- Keep business logic out of the UI code-behind.

---

## See Also
- `Calibrator/Services/ConfigService.cs`
- `Calibrator/Services/WelderServiceRefactored.cs`
- `WelderRS232/WelderCommunicationService.cs`
- `Logger/Logger.cs`
- `Calibrator/MainWindow.xaml(.cs)`

## New Architecture Components

### 1. ConfigService (New)
- **Location**: `Calibrator/Services/ConfigService.cs`
- **Purpose**: Singleton service for managing configuration settings accessible by all layers
- **Features**:
  - Manages `WelderSettings` (communication type, COM port, TCP/IP settings)
  - Manages `DetectedPort` list (ports found during scanning)
  - Provides events for settings and detected ports changes
  - Persists configuration to JSON files
  - Thread-safe singleton pattern

### 2. ServiceContainer (Updated)
- **Location**: `Calibrator/Services/ServiceContainer.cs`
- **Purpose**: Dependency injection container for all services
- **Updates**:
  - Added `ConfigService` property
  - Added `InitializeAsync()` method to initialize all services
  - Added `IsInitialized` property to track initialization state

### 3. WelderServiceRefactored (New)
- **Location**: `Calibrator/Services/WelderServiceRefactored.cs`
- **Purpose**: Refactored WelderService that follows the new architecture
- **Key Changes**:
  - Uses `WelderCommunicationManager` instead of direct communication
  - Integrates with `ConfigService` for settings management
  - Subscribes to ConfigService events
  - Updates detected ports in ConfigService during scanning
  - Follows the layered architecture pattern

### 4. MainWindow (Updated)
- **Location**: `Calibrator/MainWindow.xaml.cs`
- **Updates**:
  - Added `InitializeServicesAsync()` method
  - Subscribes to ConfigService events
  - Updated to handle nullable WelderService
  - Proper service initialization sequence

## Architecture Layers

### Layer 1: WPF UI
- **Components**: MainWindow, User Controls
- **Responsibilities**: User interface, event handling, display data
- **Dependencies**: WelderService, ConfigService

### Layer 2: WelderService
- **Components**: WelderServiceRefactored
- **Responsibilities**: Business logic, statistics, history management
- **Dependencies**: WelderCommunicationManager, ConfigService

### Layer 3: Welder
- **Components**: Welder class (existing)
- **Responsibilities**: Welder-specific operations, command building
- **Dependencies**: WelderCommunicationService

### Layer 4: WelderCommunicationService
- **Components**: WelderCommunicationService (existing)
- **Responsibilities**: Abstract communication layer
- **Dependencies**: IWelderCommunication implementations

### Layer 5: Communication Implementations
- **Components**: SerialWelderCommunication, TcpWelderCommunication
- **Responsibilities**: Specific communication protocols
- **Dependencies**: SerialPort, USRDeviceManager

### Layer 6: Hardware
- **Components**: SerialPort, USRDeviceManager
- **Responsibilities**: Direct hardware communication

### Layer 7: ConfigService (Cross-cutting)
- **Components**: ConfigService
- **Responsibilities**: Configuration management accessible by all layers
- **Dependencies**: None (singleton)

## Key Benefits

1. **Separation of Concerns**: Each layer has a specific responsibility
2. **Testability**: Services can be easily mocked and tested
3. **Maintainability**: Clear dependencies and interfaces
4. **Extensibility**: Easy to add new communication protocols
5. **Configuration Management**: Centralized configuration accessible by all layers
6. **Event-Driven**: Services communicate through events

## Migration Path

### Current State
- Original `WelderService` still exists for backward compatibility
- New `WelderServiceRefactored` implements the new architecture
- `ConfigService` is available for all layers
- `ServiceContainer` manages service initialization

### Next Steps
1. Test the new architecture with real hardware
2. Gradually migrate UI components to use the new services
3. Remove old WelderService once migration is complete
4. Add unit tests for the new architecture

## Configuration Files

### welder_settings.json
```json
{
  "CommType": "COM",
  "COM_Port": "COM3",
  "COM_Baud": 115200,
  "USR_IP": "192.168.0.7",
  "USR_Port": 23,
  "PreferTcpIp": false
}
```

### detected_ports.json
```json
[
  {
    "Name": "COM3",
    "Type": 0,
    "BaudRate": 115200,
    "IsConnected": true,
    "LastDetected": "2024-01-01T12:00:00",
    "Response": "ZGRZ"
  }
]
```

## Event Flow

1. **UI Action** → MainWindow calls WelderService method
2. **WelderService** → Uses WelderCommunicationManager for communication
3. **WelderCommunicationManager** → Uses WelderCommunicationService
4. **WelderCommunicationService** → Uses specific communication implementation
5. **Communication Implementation** → Communicates with hardware
6. **ConfigService** → Provides configuration to all layers
7. **Events** → Notify UI of changes

## Error Handling

- Each layer handles its own errors
- Errors are logged through LoggerService
- UI is notified of errors through events
- Graceful degradation when services are unavailable

## Future Enhancements

1. **Plugin Architecture**: Easy to add new communication protocols
2. **Configuration UI**: Visual configuration editor
3. **Remote Configuration**: Network-based configuration management
4. **Advanced Logging**: Structured logging with different levels
5. **Performance Monitoring**: Metrics collection and monitoring 