﻿#include "PipeClient.h"

#include "UE4Manager.h"
#include "Utilities/Logger.h"

#define BUFFER_SIZE 512

namespace UnrealVR
{
    bool PipeClient::Init()
    {
        hPipe = CreateFile(
            TEXT("\\\\.\\pipe\\UnrealVR"),
            GENERIC_READ,
            0,
            nullptr,
            OPEN_EXISTING,
            0,
            nullptr
        );
        if (hPipe == INVALID_HANDLE_VALUE)
        {
            Log::Error("[UnrealVR] Couldn't open named pipe; error %d", GetLastError());
            return false;
        }
        CreateThread(nullptr, 0, InitThread, nullptr, 0, nullptr);
        Log::Info("[UnrealVR] Opened named pipe to app UI for changing settings in realtime");
        return true;
    }

    DWORD PipeClient::InitThread(LPVOID)
    {
        CHAR chBuf[BUFFER_SIZE];
        DWORD cbRead;
        while (ReadFile(hPipe, chBuf, BUFFER_SIZE * sizeof(CHAR), &cbRead, nullptr))
            HandleCommand(chBuf);
        Log::Error("[UnrealVR] Named pipe closed; error %d", GetLastError());
        return NULL;
    }

    void PipeClient::HandleCommand(CHAR buffer[])
    {
        switch(static_cast<Setting>(buffer[0]))
        {
        case Setting::CmUnitsScale:
            memcpy(&UE4Manager::CmUnitsScale, &buffer[1], 4 * sizeof(CHAR));
            Log::Info("[UnrealVR] Set CmUnitsScale to %.3f", UE4Manager::CmUnitsScale);
            break;
        case Setting::FOVScale:
            memcpy(&UE4Manager::FOVScale, &buffer[1], 4 * sizeof(CHAR));
            Log::Info("[UnrealVR] Set FOVScale to %.3f", UE4Manager::FOVScale);
            break;
        }
    }

    void PipeClient::Stop()
    {
        CloseHandle(hPipe);
    }
}
