# FlipsiColor — Test-Baum

# Quell-Dateien mit absoluten Pfaden (SOURCES ist relativ zum Projekt-Root)
set(TEST_SOURCES)
foreach(SRC ${SOURCES})
    list(APPEND TEST_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../${SRC}")
endforeach()

set(TEST_HEADERS)
foreach(HDR ${HEADERS})
    list(APPEND TEST_HEADERS "${CMAKE_CURRENT_SOURCE_DIR}/../${HDR}")
endforeach()

add_executable(test_flipsicolor
    test_main.cpp
    ${TEST_SOURCES}
    ${TEST_HEADERS}
)

target_include_directories(test_flipsicolor PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/../include
    ${CMAKE_CURRENT_BINARY_DIR}/..
)

target_link_libraries(test_flipsicolor PRIVATE
    Qt6::Test
    Qt6::Core
    Qt6::Gui
    Qt6::Quick
    Qt6::QuickControls2
    Qt6::Widgets
    Qt6::Network
    opencv_core
    opencv_imgproc
    opencv_imgcodecs
    opencv_photo
    opencv_video
    onnxruntime
    LibRaw::LibRaw
    Lensfun::Lensfun
    FFMPEG::avcodec
    FFMPEG::avformat
    FFMPEG::avutil
    FFMPEG::swscale
    FFMPEG::swresample
    LCMS2::LCMS2
    LibExif::LibExif
)

add_test(NAME flipsicolor_unit COMMAND test_flipsicolor)