// FlipsiColor — GPU-Informationen Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/utils/GPUInfo.h>
#include <QDebug>

#ifdef Q_OS_WIN
#include <windows.h>
#include <dxgi.h>
#endif

namespace flipsicolor {

GPUInfo::GPUInfo(QObject* parent)
    : QObject(parent)
{
}

QString GPUInfo::gpuName() const
{
#ifdef Q_OS_WIN
    // DXGI Adapter Enumeration
    IDXGIFactory* factory = nullptr;
    if (SUCCEEDED(CreateDXGIFactory(__uuidof(IDXGIFactory), reinterpret_cast<void**>(&factory)))) {
        IDXGIAdapter* adapter = nullptr;
        if (SUCCEEDED(factory->EnumAdapters(0, &adapter))) {
            DXGI_ADAPTER_DESC desc;
            adapter->GetDesc(&desc);
            QString name = QString::fromWCharArray(desc.Description);
            adapter->Release();
            factory->Release();
            return name;
        }
        factory->Release();
    }
#elif defined(Q_OS_LINUX)
    // /proc/driver/nvidia/gpu existiert?
    // Oder vulkaninfo
    return QStringLiteral("GPU (Linux)");
#elif defined(Q_OS_MACOS)
    return QStringLiteral("Apple GPU (Metal)");
#endif
    return QStringLiteral("Unbekannt");
}

bool GPUInfo::istVerfuegbar() const
{
    return !gpuName().isEmpty() && gpuName() != "Unbekannt";
}

int GPUInfo::vramMB() const
{
    // TODO: Plattformspezifische Implementierung
    return 0;
}

GPUInfo::Backend GPUInfo::bestesBackend() const
{
#ifdef Q_OS_WIN
    return Backend::DirectML;
#elif defined(Q_OS_MACOS)
    return Backend::Metal;
#else
    // Prüfe CUDA
    return Backend::CUDA;
#endif
}

} // namespace flipsicolor
