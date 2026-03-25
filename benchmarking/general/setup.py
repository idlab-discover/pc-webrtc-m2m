from setuptools import find_packages, setup


if __name__ == "__main__":
    setup(
        name="benchmarking-suite",
        version="0.1.0",
        package_dir={"": "src"},
        packages=find_packages(where="src"),
    )
