cmake_minimum_required(VERSION 3.24)
project(FlipsiColor
    VERSION 0.1.0
    DESCRIPTION "KI-gestützte Bild- & Videofarbkorrektur Desktop-App"
    LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# ── Optionen ─────────────────────────────────────────────────────────────────
option(FLIPSICOLOR_BUILD_TESTS "Tests bauen" ON)
option(FLIPSICOLOR_USE_VCPKG "vcpkg für Abhängigkeiten verwenden" OFF)
option(FLIPSICOLOR_STATIC_LINK "Bibliotheken statisch verknüpfen" OFF)

# ── Qt ───────────────────────────────────────────────────────────────────────
find_package(Qt6 REQUIRED COMPONENTS Core Gui Quick QuickControls2 Widgets)
qt_standard_project_setup(REQUIRES 6.5)

# ── Abhängigkeiten ───────────────────────────────────────────────────────────
find_package(OpenCV REQUIRED COMPONENTS core imgproc photo video)
find_package(onnxruntime REQUIRED)
find_package(LibRaw REQUIRED)
find_package(Lensfun REQUIRED)
find_package(FFMPEG REQUIRED COMPONENTS avcodec avformat avutil swscale swresample)
find_package(LCMS2 REQUIRED)

# ── Quellen ──────────────────────────────────────────────────────────────────
add_subdirectory(src)

# ── Ressourcen ──────────────────────────────────────────────────────────────
qt_add_resources(RESOURCES resources/resources.qrc)

# ── Ausführbare Datei ──────────────────────────────────────────────────────
qt_add_executable(flipsicolor
    main.cpp
    ${SOURCES}
    ${RESOURCES}
)

target_include_directories(flipsicolor PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_BINARY_DIR}
)

target_link_libraries(flipsicolor PRIVATE
    Qt6::Core
    Qt6::Gui
    Qt6::Quick
    Qt6::QuickControls2
    Qt6::Widgets
    opencv_core
    opencv_imgproc
    opencv_photo
    opencv_video
    onnxruntime
    lensfun
    ${FFMPEG_LIBRARIES}
    lcms2
)

# ── Installation ────────────────────────────────────────────────────────────
install(TARGETS flipsicolor
    RUNTIME DESTINATION bin
    BUNDLE DESTINATION .
)

# ── Tests ───────────────────────────────────────────────────────────────────
if(FLIPSICOLOR_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()