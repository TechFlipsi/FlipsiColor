# FlipsiColor — Test-Baum

add_executable(test_flipsicolor
    test_main.cpp
)

target_include_directories(test_flipsicolor PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/../include
)

target_link_libraries(test_flipsicolor PRIVATE
    Qt6::Test
    Qt6::Core
)

add_test(NAME flipsicolor_unit COMMAND test_flipsicolor)