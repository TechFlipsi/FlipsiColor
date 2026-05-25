# FlipsiColor — Quell-Baum

set(SOURCES
    # Kern
    src/core/Application.cpp
    src/core/Pipeline.cpp
    src/core/Project.cpp
    src/core/Settings.cpp

    # KI
    src/ai/ModelManager.cpp
    src/ai/InferenceEngine.cpp
    src/ai/ModelDownloader.cpp

    # Farbe
    src/color/ColorManager.cpp
    src/color/LensCorrector.cpp
    src/color/StyleLUT.cpp
    src/color/WhiteBalance.cpp

    # Bild
    src/image/ImagePipeline.cpp
    src/image/RawDecoder.cpp
    src/image/ExifReader.cpp

    # Video
    src/video/VideoPipeline.cpp
    src/video/FrameProcessor.cpp
    src/video/SceneDetector.cpp

    # UI
    src/ui/AppWindow.cpp
    src/ui/ImageEditor.cpp
    src/ui/VideoEditor.cpp
    src/ui/SettingsPanel.cpp
    src/ui/LearningUI.cpp

    # Hilfsprogramme
    src/utils/Logger.cpp
    src/utils/GPUInfo.cpp
    src/utils/FileSystem.cpp
    src/utils/AutoUpdater.cpp

    PARENT_SCOPE
)

set(HEADERS
    # Kern
    include/flipsicolor/core/Application.h
    include/flipsicolor/core/Pipeline.h
    include/flipsicolor/core/Project.h
    include/flipsicolor/core/Settings.h

    # KI
    include/flipsicolor/ai/InferenceEngine.h
    include/flipsicolor/ai/ModelManager.h
    include/flipsicolor/ai/ModelDownloader.h

    # Farbe
    include/flipsicolor/color/ColorManager.h
    include/flipsicolor/color/LensCorrector.h
    include/flipsicolor/color/StyleLUT.h

    # Bild
    include/flipsicolor/image/ImagePipeline.h
    include/flipsicolor/image/RawDecoder.h
    include/flipsicolor/image/ExifReader.h

    # Video
    include/flipsicolor/video/VideoPipeline.h
    include/flipsicolor/video/FrameProcessor.h
    include/flipsicolor/video/SceneDetector.h

    # UI
    include/flipsicolor/ui/AppWindow.h
    include/flipsicolor/ui/ImageEditor.h
    include/flipsicolor/ui/VideoEditor.h
    include/flipsicolor/ui/SettingsPanel.h
    include/flipsicolor/ui/LearningUI.h

    # Hilfsprogramme
    include/flipsicolor/utils/Logger.h
    include/flipsicolor/utils/GPUInfo.h
    include/flipsicolor/utils/FileSystem.h
    include/flipsicolor/utils/AutoUpdater.h

    # Präcompiler-Header (MSVC C++20 <ctime>-Fix)
    include/flipsicolor/pch.h

    PARENT_SCOPE
)