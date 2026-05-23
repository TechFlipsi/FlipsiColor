# FlipsiColor — Source tree

set(SOURCES
    # Core
    src/core/Application.cpp
    src/core/Pipeline.cpp
    src/core/Project.cpp
    src/core/Settings.cpp

    # AI
    src/ai/ModelManager.cpp
    src/ai/InferenceEngine.cpp
    src/ai/ModelDownloader.cpp

    # Color
    src/color/ColorManager.cpp
    src/color/LensCorrector.cpp
    src/color/StyleLUT.cpp
    src/color/WhiteBalance.cpp

    # Image
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

    # Utils
    src/utils/Logger.cpp
    src/utils/GPUInfo.cpp
    src/utils/FileSystem.cpp

    PARENT_SCOPE
)