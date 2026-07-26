.PHONY: help deploy sync-remote build-all import-local import-remote deploy-local deploy-remote restart-local restart-remote verify-local verify-remote verify-all sync-remote status

# Default target
help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-20s\033[0m %s\n", $$1, $$2}'

# =============================================================================
# BUILD TARGETS
# =============================================================================

build-agent: ## Build terraria-agent Docker image
	docker build -t terraria-agent:latest ./terraria-agent/

build-homepage: ## Build homepage Docker image
	docker build -t homepage:latest ./homepage/

build-investigation-frontend: ## Build investigation-team-frontend Docker image
	docker build -t investigation-team-frontend:latest ./investigation-team-frontend/

build-supermarket-frontend: ## Build supermarket-frontend Docker image
	docker build -t supermarket-frontend:latest ./supermarket-frontend/

build-supermarket-api: ## Build supermarket-api Docker image
	docker build -t supermarket-api:latest ./supermarket-api/

build-investigation-api: ## Build investigation-team-api Docker image
	docker build -t investigation-team-api:latest ./investigation-team-api/

build-all: build-agent build-homepage build-investigation-frontend build-supermarket-frontend build-supermarket-api build-investigation-api ## Build all Docker images

# =============================================================================
# IMPORT
# =============================================================================

import-local: build-all ## Import all images to local k3s
	docker save terraria-agent:latest | sudo k3s ctr images import -
	docker save homepage:latest | sudo k3s ctr images import -
	docker save investigation-team-frontend:latest | sudo k3s ctr images import -
	docker save supermarket-frontend:latest | sudo k3s ctr images import -
	docker save supermarket-api:latest | sudo k3s ctr images import -
	docker save investigation-team-api:latest | sudo k3s ctr images import -

import-remote: build-all ## Import all images to remote k3s
	docker save terraria-agent:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
	docker save homepage:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
	docker save investigation-team-frontend:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
	docker save supermarket-frontend:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
	docker save supermarket-api:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
	docker save investigation-team-api:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"

# =============================================================================
# LOCAL DEPLOYMENT
# =============================================================================

deploy-local: import-local ## Deploy all services to local cluster
	sudo k3s kubectl apply -f terraria-server/namespace.yaml
	sudo k3s kubectl apply -f terraria-server/local-ingress.yaml
	sudo k3s kubectl apply -f terraria-agent/k8s/
	sudo k3s kubectl apply -f homepage/k8s/
	sudo k3s kubectl apply -f investigation-team-api/k8s/
	sudo k3s kubectl apply -f investigation-team-frontend/k8s/
	sudo k3s kubectl apply -f investigation-team-chat-backend/k8s/
	sudo k3s kubectl apply -f supermarket-api/k8s/
	sudo k3s kubectl apply -f supermarket-frontend/k8s/
	@echo "✅ Local deployment complete"

restart-local: ## Restart all deployments locally
	sudo k3s kubectl rollout restart deployment/terraria-agent -n terraria
	sudo k3s kubectl rollout restart deployment/homepage -n homepage
	sudo k3s kubectl rollout restart deployment/investigation-team-frontend -n investigation-team-frontend
	sudo k3s kubectl rollout restart deployment/investigation-team-chat-api -n investigation-team-frontend
	sudo k3s kubectl rollout restart deployment/supermarket-frontend -n supermarket
	sudo k3s kubectl rollout restart deployment/supermarket-api -n supermarket
	sudo k3s kubectl rollout restart deployment/investigation-team-api -n investigation-team
	@echo "✅ Local services restarted"

# =============================================================================
# REMOTE DEPLOYMENT
# =============================================================================

deploy-remote: import-remote ## Deploy all services to remote cluster
	# Ingress (with host header fix for remote)
	cat terraria-server/local-ingress.yaml | sed 's|path: /terraria-agent|host: gaming.andalusiaone.com\n    http:\n      paths:\n      - path: /terraria-agent|g' | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f -"
	# Services
	scp terraria-agent/k8s/deployment.yaml roman@srv01.gaming.andalusiaone.com:/tmp/
	scp terraria-agent/k8s/service.yaml roman@srv01.gaming.andalusiaone.com:/tmp/
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /tmp/deployment.yaml -f /tmp/service.yaml"
	scp homepage/k8s/deployment.yaml roman@srv01.gaming.andalusiaone.com:/tmp/homepage-deployment.yaml
	scp homepage/k8s/service.yaml roman@srv01.gaming.andalusiaone.com:/tmp/
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /tmp/homepage-deployment.yaml -f /tmp/service.yaml"
	scp investigation-team-api/k8s/deployment.yaml roman@srv01.gaming.andalusiaone.com:/tmp/it-api-deployment.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /tmp/it-api-deployment.yaml"
	scp investigation-team-frontend/k8s/deployment.yaml roman@srv01.gaming.andalusiaone.com:/tmp/it-frontend-deployment.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /tmp/it-frontend-deployment.yaml"
	scp supermarket-api/k8s/api-deployment.yaml roman@srv01.gaming.andalusiaone.com:/tmp/sm-api-deployment.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /tmp/sm-api-deployment.yaml"
	scp supermarket-frontend/k8s/deployment.yaml roman@srv01.gaming.andalusiaone.com:/tmp/sm-frontend-deployment.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /tmp/sm-frontend-deployment.yaml"
	@echo "✅ Remote deployment complete"

restart-remote: ## Restart all deployments remotely
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/terraria-agent -n terraria"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/homepage -n homepage"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/investigation-team-frontend -n investigation-team-frontend"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/investigation-team-chat-api -n investigation-team-frontend"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/supermarket-frontend -n supermarket"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/supermarket-api -n supermarket"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/investigation-team-api -n investigation-team"
	@echo "✅ Remote services restarted"

# =============================================================================
# SYNC (Deploy to both)
# =============================================================================

sync-remote: import-remote restart-remote ## Sync to remote
	@echo "✅ Remote synced"

# =============================================================================
# VERIFICATION
# =============================================================================

verify-local: ## Verify local services
	@echo "🔍 LOCAL CLUSTER"
	@echo "Pods:"
	@sudo k3s kubectl get pods --all-namespaces | grep -E "(terraria|homepage|investigation|supermarket)"
	@echo ""
	@echo "Ingress:"
	@sudo k3s kubectl get ingress --all-namespaces
	@echo ""
	@echo "Services:"
	@curl -s -o /dev/null -w "Homepage:      %{http_code}\n" http://172.30.138.92:30808/
	@curl -s -o /dev/null -w "Agent Swagger: %{http_code}\n" http://172.30.138.92:30808/terraria-agent/swagger/
	@curl -s -o /dev/null -w "Agent Health:  %{http_code}\n" http://172.30.138.92:30808/terraria-agent/health
	@curl -s -o /dev/null -w "IT Frontend:   %{http_code}\n" http://172.30.138.92:30808/it/
	@curl -s -o /dev/null -w "IT API:        %{http_code}\n" http://172.30.138.92:30808/api/health
	@curl -s -o /dev/null -w "SM Frontend:   %{http_code}\n" http://172.30.138.92:30808/supermarket/
	@curl -s -o /dev/null -w "SM API:        %{http_code}\n" http://172.30.138.92:30808/supermarket-api/

verify-remote: ## Verify remote services
	@echo "🔍 REMOTE CLUSTER"
	@echo "Pods:"
	@ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl get pods --all-namespaces | grep -E '(terraria|homepage|investigation|supermarket)'"
	@echo ""
	@echo "Services:"
	@curl -s -o /dev/null -w "Homepage:      %{http_code}\n" http://gaming.andalusiaone.com:30808/
	@curl -s -o /dev/null -w "Agent Swagger: %{http_code}\n" http://gaming.andalusiaone.com:30808/terraria-agent/swagger/
	@curl -s -o /dev/null -w "IT Frontend:   %{http_code}\n" http://gaming.andalusiaone.com:30808/it/
	@curl -s -o /dev/null -w "SM Frontend:   %{http_code}\n" http://gaming.andalusiaone.com:30808/supermarket/

verify-all: verify-local verify-remote ## Verify both clusters

# =============================================================================
# QUICK COMMANDS
# =============================================================================

deploy: deploy-local sync-remote ## Full deploy to both clusters

status: ## Show status of both clusters
	@echo "=== LOCAL ==="
	@sudo k3s kubectl get pods --all-namespaces | grep -E "(terraria|homepage|investigation|supermarket)"
	@echo ""
	@echo "=== REMOTE ==="
	@ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl get pods --all-namespaces | grep -E '(terraria|homepage|investigation|supermarket)'"
