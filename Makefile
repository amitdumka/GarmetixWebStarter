.PHONY: validate validate-no-docker docker-up docker-api-no-cache docker-down logs api-logs web-logs pg-logs smoke

validate:
	./scripts/validate-local.sh

validate-no-docker:
	./scripts/validate-local.sh --skip-docker

docker-up:
	docker compose up --build

docker-api-no-cache:
	docker compose down
	docker compose build --no-cache api
	docker compose up

docker-down:
	docker compose down

logs:
	docker compose logs --tail=200

api-logs:
	docker compose logs api --tail=250

web-logs:
	docker compose logs web --tail=200

pg-logs:
	docker compose logs postgres --tail=200

smoke:
	curl -fsS http://localhost:5080/api/health
	curl -fsS http://localhost:5080/api/auth/bootstrap-status
	curl -fsS http://localhost:5080/api/setup/status
	curl -fsSI http://localhost:3000 | head -20
