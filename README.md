# Doc To JSON

## Overview

This application allows you to upload one or more files and send them to a variety of OpenAI models for extraction into a structured JSON format.

It returns a rough cost in USD, along with token counts etc.

If you provide a JSON Schema, the model can be forced to conform to it exactly.

A full request and response history is kept locally so you can review, re-use, or delete past results.

## Installation

Set your OpenAI API key in `appsettings.json` in the server project.

Build and run **only the server** project, it hosts the Client automatically.

Access the application in your browser.

## Implementation Details

Dotnet 9 Blazor Wasm front-end, MudBlazor for UI components, and traditional backend API server.

Requires the backend server, as files cannot be sent directly from Blazor WebAssembly to OpenAI.

Supports uploading multiple files in a single request to produce one combined JSON document.

Includes an in-app JSON Schema builder for defining or editing output structure. 
This can take a JSON document to create a schema automatically, and supports arbitrarily deep documents.

Stores schema and request history in browser Local Storage.

Provides a preview of uploaded files when possible (PDF or image formats).

## Supported File Types

- PDF (.pdf)
- Microsoft Word (.docx, .doc)
- CSV (.csv)
- Plain Text (.txt)
- Microsoft Excel (.xlsx, .xls)
- JSON (.json)
- Images (.png, .jpg, .jpeg)
- XML (.xml)
