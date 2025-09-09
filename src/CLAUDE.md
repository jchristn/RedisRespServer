# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build the solution
```bash
dotnet build
```

### Run the test suite
```bash
dotnet run --project RedisRespServer.Tests
```

### Clean build artifacts
```bash
dotnet clean
```

### Build in Release mode
```bash
dotnet build --configuration Release
```

## Project Architecture

This is a .NET 8.0 solution implementing a Redis RESP (Redis Serialization Protocol) server listener with event-driven architecture:

### Core Components

**RedisRespServer project** - Main library containing:
- `RespListener.cs` - TCP server that parses RESP protocol messages and raises events for different data types
- `RespDataType.cs` - Enum defining RESP data types (SimpleString, Error, Integer, BulkString, Array, Null)  
- `EventArgs.cs` - Event argument classes for all RESP events and client connection events

**RedisRespServer.Tests project** - Custom test suite containing:
- `TestProgram.cs` - Console application entry point
- `TestRunner.cs` - Orchestrates and executes all tests
- `TestServer.cs` - Test wrapper around RespListener with comprehensive logging
- `TestClient.cs` - TCP client for testing server functionality

### Key Architecture Details

- **Event-driven design**: RespListener raises specific events for each RESP data type received
- **Multi-client support**: Server handles multiple concurrent TCP connections with unique client IDs
- **Asynchronous processing**: All network operations use async/await patterns
- **Thread-safe**: Client collection and message logging use proper locking
- **Custom test framework**: No external test dependencies - uses custom TestRunner instead of xUnit/NUnit

### RESP Protocol Support

The server parses and handles all standard RESP data types:
- Simple Strings (`+OK\r\n`)
- Errors (`-Error message\r\n`) 
- Integers (`:1000\r\n`)
- Bulk Strings (`$6\r\nfoobar\r\n`)
- Arrays (`*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n`)
- Null values (`$-1\r\n` or `*-1\r\n`)

### Development Notes

- Default listening port is 6379 (Redis standard), test server uses 6380
- Server uses event handlers for different RESP message types - subscribe to specific events based on needs
- The project has nullable reference types enabled, generating warnings for uninitialized properties
- Custom test runner provides interactive console output with timestamped logging and visual indicators

## Code Style and Implementation Rules

These rules must be followed STRICTLY for all code files to maximize consistency and maintainability:

### File Organization

1. **Namespace and Using Statements**:
   - The namespace declaration should always be at the top
   - Using statements should be contained INSIDE the namespace block
   - All Microsoft and standard system library usings should be first, in alphabetical order
   - Other using statements should follow, in alphabetical order

2. **Class Region Organization**:
   - Code files containing classes should always be organized with these five regions in order:
     - `#region Public-Members`
     - `#region Private-Members` 
     - `#region Constructors-and-Factories`
     - `#region Public-Methods`
     - `#region Private-Methods`
   - There should always be an extra line break before and after each region

### Code Structure Example

```csharp
namespace ProjectName
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    public class ExampleClass
    {

        #region Public-Members

        public int PublicProperty { get; set; }

        public event EventHandler<EventArgs> PublicEvent;

        #endregion


        #region Private-Members

        private readonly object _lockObject = new object();
        private Dictionary<string, object> _privateField;

        #endregion


        #region Constructors-and-Factories

        public ExampleClass()
        {
            _privateField = new Dictionary<string, object>();
        }

        public static ExampleClass CreateInstance()
        {
            return new ExampleClass();
        }

        #endregion


        #region Public-Methods

        public void PublicMethod()
        {
            // Implementation
        }

        #endregion


        #region Private-Methods

        private void PrivateMethod()
        {
            // Implementation
        }

        #endregion

    }
}
```

### Documentation Requirements

1. **XML Documentation**:
   - All classes must have XML documentation (`/// <summary>`)
   - All public members (properties, fields, events) must have XML documentation
   - All public methods must have XML documentation
   - All constructors must have XML documentation
   - Include `<param>` tags for all parameters
   - Include `<returns>` tags for methods with return values
   - Include `<exception>` tags for methods that can throw exceptions

2. **File Organization**:
   - Each code file must contain exactly ONE entity (class, enum, interface, struct, etc.)
   - Files containing multiple entities must be split into separate files
   - File names should match the entity name (e.g., `MyClass.cs` contains `class MyClass`)

### Documentation Example

```csharp
namespace ProjectName
{
    using System;

    /// <summary>
    /// Represents a sample class that demonstrates proper documentation.
    /// </summary>
    /// <remarks>
    /// This class provides example functionality and serves as a template
    /// for documenting other classes in the codebase.
    /// </remarks>
    public class ExampleClass
    {

        #region Public-Members

        /// <summary>
        /// Gets or sets the example property value.
        /// </summary>
        /// <value>An integer representing the example value.</value>
        public int ExampleProperty { get; set; }

        /// <summary>
        /// Occurs when an example event is raised.
        /// </summary>
        public event EventHandler<EventArgs> ExampleEvent;

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleClass"/> class.
        /// </summary>
        /// <param name="initialValue">The initial value for the example property.</param>
        /// <exception cref="ArgumentException">Thrown when initialValue is negative.</exception>
        public ExampleClass(int initialValue)
        {
            if (initialValue < 0)
                throw new ArgumentException("Initial value cannot be negative.", nameof(initialValue));
            
            ExampleProperty = initialValue;
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Performs an example operation on the provided input.
        /// </summary>
        /// <param name="input">The input string to process.</param>
        /// <returns>The processed result as a string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        public string ProcessInput(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            
            return input.ToUpper();
        }

        #endregion

    }
}
```

### Enforcement

- ALL new code files must follow this structure and documentation requirements
- When editing existing files, refactor them to match this organization if they don't already
- Split any existing files that contain multiple entities into separate files
- Ensure all public APIs are properly documented
- This ensures consistency across the entire codebase and improves maintainability