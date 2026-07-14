#!/bin/bash

# Exit immediately if any command fails
set -e

echo "=================================================="
echo " Starting Headless Server Setup for Mac Mini"
echo "=================================================="

# 1. Update the system packages
echo "--> Updating system packages..."
sudo apt update && sudo apt upgrade -y

# 2. Install Nginx Web Server
echo "--> Installing Nginx..."
sudo apt install nginx -y
sudo systemctl start nginx
sudo systemctl enable nginx

# 3. Install .NET 8 Runtime (For ASP.NET APIs)
echo "--> Installing .NET 8 Runtime..."
sudo apt install -y dotnet-runtime-8.0

# 4. Install Node.js and PM2 (For Nuxt.js Web App)
echo "--> Installing Node.js and PM2..."
sudo apt install nodejs npm -y
sudo npm install pm2 -g

# 5. Install Docker Engine
echo "--> Installing Docker..."
sudo apt install docker.io -y
sudo systemctl start docker
sudo systemctl enable docker
sudo usermod -aG docker $USER

# 6. Turn Off Visual Desktop Interface (Go Headless)
echo "--> Disabling graphical desktop interface..."
sudo systemctl set-default multi-user.target

# 7. Prevent Mac Mini from going to sleep
echo "--> Configuring power management to stay awake..."
sudo sed -i 's/#HandleLidSwitch=suspend/HandleLidSwitch=ignore/g' /etc/systemd/logind.conf
sudo systemctl restart systemd-logind

# 8. Configure Automatic Command-Line Login
echo "--> Configuring automatic command-line login..."
# Target the standard TTY1 terminal interface for auto-login
sudo mkdir -p /etc/systemd/system/getty@tty1.service.d/
sudo bash -c "cat <<EOF > /etc/systemd/system/getty@tty1.service.d/override.conf
[Service]
ExecStart=
ExecStart=-/sbin/agetty --autologin $USER --noclear %I \\\$TERM
EOF"

echo "=================================================="
echo " Setup complete! Finding your Local IP Address..."
echo "=================================================="
# Extract the active local IPv4 address
LOCAL_IP=$(ip route get 1 | awk '{print $7;exit}')
echo "Your Mac Mini Server IP address is: $LOCAL_IP"
echo "You can connect to this server from another PC using: ssh $USER@$LOCAL_IP"
echo "=================================================="
echo " The system will now reboot into headless mode in 10 seconds..."
sleep 10
sudo reboot
