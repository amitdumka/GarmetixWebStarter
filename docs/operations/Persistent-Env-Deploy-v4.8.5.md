# Persistent Env Deploy v4.8.5

## One-time setup in WSL

```bash
mkdir -p ~/.garmetix
cp ~/GarmetixWebStarter/deploy/macmini.env ~/.garmetix/macmini.env
chmod 600 ~/.garmetix/macmini.env
```

Create the helper if it does not already exist:

```bash
cd ~/GarmetixWebStarter
./deploy/create-garmetix-link-env-helper.sh
```

## One-time setup on Mac mini

```bash
ssh amit@192.168.11.126
sudo mkdir -p /opt/garmetix/shared/env
sudo chown -R amit:amit /opt/garmetix/shared
chmod 750 /opt/garmetix/shared/env
cp /opt/garmetix/current/.env.production /opt/garmetix/shared/env/.env.production
chmod 600 /opt/garmetix/shared/env/.env.production
```

## Normal deploy

```bash
cd ~/GarmetixWebStarter
./deploy/deploy-to-macmini.sh
```

The deploy script now calls `~/garmetix-link-env.sh` automatically before it reads `deploy/macmini.env`, uploads the release, or starts Docker.
