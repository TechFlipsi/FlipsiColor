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

# ── i18n (Mehrsprachigkeit) ──────────────────────────────────────────────────
# Deutsch = Default, weitere Sprachen als .qm kompiliert
set(FLIPSICOLOR_LANGUAGES de en es fr it pt_BR ja zh_CN ko)
set(I18N_TS_DIR ${CMAKE_CURRENT_SOURCE_DIR}/i18n)

# ── Optionen ─────────────────────────────────────────────────────────────────
option(FLIPSICOLOR_BUILD_TESTS "Tests bauen" ON)
option(FLIPSICOLOR_USE_VCPKG "vcpkg für Abhängigkeiten verwenden" OFF)
option(FLIPSICOLOR_STATIC_LINK "Bibliotheken statisch verknüpfen" OFF)

# ── Qt ───────────────────────────────────────────────────────────────────────
find_package(Qt6 REQUIRED COMPONENTS Core Gui Quick QuickControls2 Widgets LinguistTools)
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

# ── i18n .qm kompilieren ────────────────────────────────────────────────────
set(QM_DIR ${CMAKE_CURRENT_BINARY_DIR}/i18n)
file(MAKE_DIRECTORY ${QM_DIR})

foreach(LANG ${FLIPSICOLOR_LANGUAGES})
    set(TS_FILE ${I18N_TS_DIR}/flipsicolor_${LANG}.ts)
    set(QM_FILE ${QM_DIR}/flipsicolor_${LANG}.qm)
    add_custom_command(
        OUTPUT ${QM_FILE}
        COMMAND Qt6::lrelease -compress -removeidentical ${TS_FILE} -qm ${QM_FILE}
        DEPENDS ${TS_FILE}
        COMMENT "Übersetzung kompilieren: ${LANG}"
    )
    list(APPEND QM_FILES ${QM_FILE})
endforeach()

# Benutzerdefiniertes Ziel: Alle .qm kompilieren
add_custom_target(translations ALL DEPENDS ${QM_FILES})

# .qm Dateien zum Build-Output hinzufügen (Installation als seperate Dateien)
foreach(QM ${QM_FILES})
    install(FILES ${QM} DESTINATION share/flipsicolor/i18n)
endforeach()

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