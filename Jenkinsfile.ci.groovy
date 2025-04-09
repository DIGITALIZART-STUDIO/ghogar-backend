pipeline {
	agent any
	environment {
		BUILD_REF = "${sh(script: "echo -n '${BUILD_TAG}' | sha256sum | cut -c1-12", returnStdout: true).trim()}"
	}
	stages {
		stage('Build backend') {
			steps {
				dir("src") {
					// Just use the docker image to build the frontend
					sh "docker build -t gestionhogar-backend-ci-${BUILD_REF} -f Deployment/Dockerfile.alpine ."
				}
			}
		}
	}
	post {
		always {
			// remove docker images/containers built
			sh "docker rm gestionhogar-backend-ci-${BUILD_REF} || true"
			sh "docker rmi gestionhogar-backend-ci-${BUILD_REF} || true"
		}
	}
}
